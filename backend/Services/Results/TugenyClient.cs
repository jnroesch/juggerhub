using System.Globalization;
using System.Text.Json;
using JuggerHub.Common;
using JuggerHub.Resilience;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace JuggerHub.Services.Results;

/// <summary>How a call to Tugeny ended.</summary>
public enum TugenyOutcome
{
    Ok,

    /// <summary>Tugeny answered, and there is no such tournament (its body was <c>null</c>).</summary>
    NotFound,

    /// <summary>Tugeny could not be reached, timed out, refused, or its breaker is open.</summary>
    Unavailable,

    /// <summary>Tugeny answered with something this client cannot read (or too much of it).</summary>
    Unusable,
}

/// <summary>A Tugeny response: an outcome, and a value when it is <see cref="TugenyOutcome.Ok"/>.</summary>
public sealed record TugenyResponse<T>(TugenyOutcome Outcome, T? Value)
{
    public static TugenyResponse<T> Ok(T value) => new(TugenyOutcome.Ok, value);

    public static TugenyResponse<T> Fail(TugenyOutcome outcome) => new(outcome, default);
}

/// <summary>A Tugeny tournament, as needed to confirm a link.</summary>
public sealed record TugenyTournament(int Id, string Slug, string Name, DateOnly? StartDate);

/// <summary>One line of a finalized Tugeny ranking.</summary>
public sealed record TugenyRankedTeam(int Position, int TeamId, string TeamName);

/// <summary>One Tugeny match. <see cref="Timestamp"/> is local wall-clock text, used only for order.</summary>
public sealed record TugenyMatch(
    int ResponseIndex,
    string? Group,
    string Name,
    string? Timestamp,
    int FirstTeamId,
    int SecondTeamId,
    string FirstTeam,
    string SecondTeam,
    int? WinnerTeamId,
    int[] FirstScores,
    int[] SecondScores);

/// <summary>Reads Tugeny's public, MIT-licensed data interface (feature 050). The only code that talks to Tugeny.</summary>
public interface ITugenyClient
{
    Task<TugenyResponse<TugenyTournament>> GetTournamentBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>The final ranking; an empty list means the tournament is not finalized in Tugeny.</summary>
    Task<TugenyResponse<IReadOnlyList<TugenyRankedTeam>>> GetRankingAsync(int tournamentId, CancellationToken ct = default);

    Task<TugenyResponse<IReadOnlyList<TugenyMatch>>> GetMatchesAsync(int tournamentId, CancellationToken ct = default);
}

/// <inheritdoc />
/// <remarks>
/// <para>
/// Uses the named <c>"Tugeny"</c> client, which carries the shared resilience pipeline (bounded
/// attempts and total time, jittered retry of transient faults, a breaker tuned to this integration's
/// volume) and the response size guard. Nothing here retries, waits or times out on its own.
/// </para>
/// <para>
/// Parsing is tolerant of what does not matter and strict about what does, per the measured shapes in
/// specs/050-tournament-results/contracts/tugeny.md §1: unknown keys are ignored, numbers may arrive as
/// strings, an unreadable score becomes an empty score rather than a failed import — but a missing
/// required field makes the whole answer unusable, so nothing half-read is ever saved.
/// </para>
/// <para>
/// Logs carry the endpoint, the status and the length — never a body (Principle VII).
/// </para>
/// </remarks>
public sealed class TugenyClient : ITugenyClient
{
    private const string Api = "api/persistent/";

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<TugenyClient> _logger;

    public TugenyClient(IHttpClientFactory factory, ILogger<TugenyClient> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<TugenyResponse<TugenyTournament>> GetTournamentBySlugAsync(string slug, CancellationToken ct = default)
    {
        var (outcome, root) = await GetJsonAsync("tournamentsBySlug", $"{Api}tournamentsBySlug/{Uri.EscapeDataString(slug)}", ct);
        if (outcome != TugenyOutcome.Ok)
        {
            return TugenyResponse<TugenyTournament>.Fail(outcome);
        }

        // Tugeny answers an unknown slug with HTTP 200 and the body `null`.
        if (root.ValueKind == JsonValueKind.Null)
        {
            return TugenyResponse<TugenyTournament>.Fail(TugenyOutcome.NotFound);
        }

        if (root.ValueKind != JsonValueKind.Object
            || ReadInt(root, "id") is not { } id
            || ReadString(root, "name") is not { } name)
        {
            return Unusable<TugenyTournament>("tournamentsBySlug");
        }

        var start = ReadString(root, "startdate");
        DateOnly? startDate = start is { Length: >= 10 }
            && DateOnly.TryParseExact(start[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

        return TugenyResponse<TugenyTournament>.Ok(new TugenyTournament(id, ReadString(root, "slug") ?? slug, name, startDate));
    }

    public async Task<TugenyResponse<IReadOnlyList<TugenyRankedTeam>>> GetRankingAsync(int tournamentId, CancellationToken ct = default)
    {
        var (outcome, root) = await GetJsonAsync("rankingsByTournamentId", $"{Api}rankingsByTournamentId/{tournamentId}?returnType=json", ct);
        if (outcome != TugenyOutcome.Ok)
        {
            return TugenyResponse<IReadOnlyList<TugenyRankedTeam>>.Fail(outcome);
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("rankings", out var rankings))
        {
            return Unusable<IReadOnlyList<TugenyRankedTeam>>("rankingsByTournamentId");
        }

        // Not finalized: Tugeny serves `"rankings": []`.
        if (rankings.ValueKind == JsonValueKind.Array)
        {
            return rankings.GetArrayLength() == 0
                ? TugenyResponse<IReadOnlyList<TugenyRankedTeam>>.Ok([])
                : Unusable<IReadOnlyList<TugenyRankedTeam>>("rankingsByTournamentId");
        }

        // Finalized: an object keyed "1".."N".
        if (rankings.ValueKind != JsonValueKind.Object)
        {
            return Unusable<IReadOnlyList<TugenyRankedTeam>>("rankingsByTournamentId");
        }

        var teams = new List<TugenyRankedTeam>();
        foreach (var entry in rankings.EnumerateObject())
        {
            if (!int.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var position)
                || position < 1
                || entry.Value.ValueKind != JsonValueKind.Object
                || ReadInt(entry.Value, "team_id") is not { } teamId
                || ReadString(entry.Value, "team_name") is not { } teamName)
            {
                return Unusable<IReadOnlyList<TugenyRankedTeam>>("rankingsByTournamentId");
            }

            teams.Add(new TugenyRankedTeam(position, teamId, teamName));
        }

        return TugenyResponse<IReadOnlyList<TugenyRankedTeam>>.Ok(teams.OrderBy(t => t.Position).ToList());
    }

    public async Task<TugenyResponse<IReadOnlyList<TugenyMatch>>> GetMatchesAsync(int tournamentId, CancellationToken ct = default)
    {
        var (outcome, root) = await GetJsonAsync("matches", $"{Api}matches?tournamentIds={tournamentId}&returnType=json", ct);
        if (outcome != TugenyOutcome.Ok)
        {
            return TugenyResponse<IReadOnlyList<TugenyMatch>>.Fail(outcome);
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            return Unusable<IReadOnlyList<TugenyMatch>>("matches");
        }

        var matches = new List<TugenyMatch>();
        var index = 0;
        foreach (var m in root.EnumerateArray())
        {
            if (m.ValueKind != JsonValueKind.Object
                || ReadString(m, "name") is not { } name
                || ReadInt(m, "first_team_id") is not { } firstId
                || ReadInt(m, "second_team_id") is not { } secondId
                || ReadString(m, "first_team") is not { } first
                || ReadString(m, "second_team") is not { } second)
            {
                return Unusable<IReadOnlyList<TugenyMatch>>("matches");
            }

            var (firstScores, secondScores) = ParseScore(ReadString(m, "score_total"));
            matches.Add(new TugenyMatch(
                index++,
                ReadString(m, "group"),
                name,
                ReadString(m, "timestamp"),
                firstId,
                secondId,
                first,
                second,
                ReadInt(m, "victorious_team_id"),
                firstScores,
                secondScores));
        }

        return TugenyResponse<IReadOnlyList<TugenyMatch>>.Ok(matches);
    }

    /// <summary>
    /// Parses Tugeny's score text — "5:1 - 2:5 - 0:5" — into per-set points for each side. Anything
    /// unreadable yields empty scores: the match is still worth showing.
    /// </summary>
    internal static (int[] First, int[] Second) ParseScore(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ([], []);
        }

        var first = new List<int>();
        var second = new List<int>();
        foreach (var set in text.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var points = set.Split(':', StringSplitOptions.TrimEntries);
            if (points.Length != 2
                || !int.TryParse(points[0], NumberStyles.None, CultureInfo.InvariantCulture, out var a)
                || !int.TryParse(points[1], NumberStyles.None, CultureInfo.InvariantCulture, out var b))
            {
                return ([], []);
            }

            first.Add(a);
            second.Add(b);
        }

        return ([.. first], [.. second]);
    }

    private async Task<(TugenyOutcome Outcome, JsonElement Root)> GetJsonAsync(string endpoint, string relative, CancellationToken ct)
    {
        var client = _factory.CreateClient(TugenyOptions.ResilienceName);
        try
        {
            using var response = await client.GetAsync(relative, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tugeny {Endpoint} answered {Status}.", endpoint, (int)response.StatusCode);
                return (TugenyOutcome.Unavailable, default);
            }

            var body = await response.Content.ReadAsByteArrayAsync(ct);
            try
            {
                using var document = JsonDocument.Parse(body);
                return (TugenyOutcome.Ok, document.RootElement.Clone());
            }
            catch (JsonException)
            {
                _logger.LogWarning("Tugeny {Endpoint} answered {Length} bytes that are not JSON.", endpoint, body.Length);
                return (TugenyOutcome.Unusable, default);
            }
        }
        catch (ResponseTooLargeException ex)
        {
            _logger.LogWarning("Tugeny {Endpoint} answered more than {Limit} bytes; refused without retrying.", endpoint, ex.MaxBytes);
            return (TugenyOutcome.Unusable, default);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested
            && ex is HttpRequestException or TimeoutRejectedException or BrokenCircuitException or TaskCanceledException)
        {
            // The pipeline has already retried and given up; the pipeline's own telemetry has the detail.
            _logger.LogWarning("Tugeny {Endpoint} could not be reached ({Reason}).", endpoint, ex.GetType().Name);
            return (TugenyOutcome.Unavailable, default);
        }
    }

    private TugenyResponse<T> Unusable<T>(string endpoint)
    {
        _logger.LogWarning("Tugeny {Endpoint} answered in a shape this client cannot read.", endpoint);
        return TugenyResponse<T>.Fail(TugenyOutcome.Unusable);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>An integer written as a number or as digits; null when absent, null or anything else.</summary>
    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => null,
        };
    }
}
