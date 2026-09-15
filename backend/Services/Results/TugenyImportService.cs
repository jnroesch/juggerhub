using JuggerHub.Data;
using JuggerHub.Dtos.Results;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Results;

/// <summary>Linking a tournament event to its Tugeny tournament, and importing its results (feature 050, US4).</summary>
public interface ITugenyImportService
{
    Task<ResultOutcome<TugenyLinkedDto>> LinkAsync(Guid eventId, Guid userId, string address, CancellationToken ct = default);

    Task<ResultOutcome<bool>> UnlinkAsync(Guid eventId, Guid userId, CancellationToken ct = default);

    /// <summary>A stateless draft of the import — nothing is saved.</summary>
    Task<ResultOutcome<TugenyImportPreviewDto>> PreviewAsync(Guid eventId, Guid userId, CancellationToken ct = default);

    /// <summary>Re-fetch from Tugeny and replace the ranking and matches with what it serves.</summary>
    Task<ResultOutcome<TournamentResultDto>> CommitAsync(
        Guid eventId, Guid userId, IReadOnlyList<ImportConnectionRequest> connections, CancellationToken ct = default);
}

/// <inheritdoc />
/// <remarks>
/// <para>
/// <b>Provenance is server-established</b> (research R7): the commit fetches from Tugeny again rather
/// than accepting imported rows from the browser, so what is stored and labelled "from Tugeny" is what
/// Tugeny served. That is safe because a tournament Tugeny serves is finalized, and Tugeny finalizes a
/// tournament only once.
/// </para>
/// <para>
/// Every connection to a JuggerHub team is an explicit choice sent with the commit and applies to that
/// commit only; it goes only to a team with a confirmed JuggerHub sign-up for this event. No mapping is
/// remembered, reused or suggested across imports or tournaments (owner decision, FR-025).
/// </para>
/// </remarks>
public sealed class TugenyImportService : ITugenyImportService
{
    private readonly AppDbContext _db;
    private readonly ITugenyClient _tugeny;
    private readonly ITournamentResultService _results;

    public TugenyImportService(AppDbContext db, ITugenyClient tugeny, ITournamentResultService results)
    {
        _db = db;
        _tugeny = tugeny;
        _results = results;
    }

    public async Task<ResultOutcome<TugenyLinkedDto>> LinkAsync(Guid eventId, Guid userId, string address, CancellationToken ct = default)
    {
        var ev = await ResultEvents.LoadAsync(_db, eventId, userId, ct);
        if (LinkGate(ev) is { } gate)
        {
            return ResultOutcome<TugenyLinkedDto>.Fail(gate);
        }

        if (!TugenyLinkParser.TryParseSlug(address, out var slug))
        {
            return ResultOutcome<TugenyLinkedDto>.Fail(ResultStatus.Invalid, "That is not a tugeny.org tournament address.");
        }

        var found = await _tugeny.GetTournamentBySlugAsync(slug, ct);
        switch (found.Outcome)
        {
            case TugenyOutcome.NotFound:
                return ResultOutcome<TugenyLinkedDto>.Fail(ResultStatus.TugenyNotFound);
            case not TugenyOutcome.Ok:
                return ResultOutcome<TugenyLinkedDto>.Fail(ResultStatus.TugenyUnavailable);
        }

        var tournament = found.Value!;
        var result = await _db.TournamentResults.FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        if (result is null)
        {
            result = new TournamentResult { EventId = eventId };
            _db.TournamentResults.Add(result);
        }

        result.TugenyTournamentId = tournament.Id;
        result.TugenySlug = tournament.Slug.Length <= TugenyLinkParser.MaxSlugLength ? tournament.Slug : slug;
        result.TugenyName = Truncate(tournament.Name, 200);
        result.TugenyStartDate = tournament.StartDate;
        await _db.SaveChangesAsync(ct);

        // A warning, not a rule: linking one Tugeny tournament twice is usually a duplicate event.
        var elsewhere = await _db.TournamentResults
            .AnyAsync(r => r.TugenyTournamentId == tournament.Id && r.EventId != eventId, ct);

        return ResultOutcome<TugenyLinkedDto>.Ok(new TugenyLinkedDto(
            tournament.Id, result.TugenySlug, result.TugenyName, tournament.StartDate, elsewhere));
    }

    public async Task<ResultOutcome<bool>> UnlinkAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var ev = await ResultEvents.LoadAsync(_db, eventId, userId, ct);
        if (ev is null)
        {
            return ResultOutcome<bool>.Fail(ResultStatus.NotFound);
        }

        if (!ev.IsAdmin)
        {
            return ResultOutcome<bool>.Fail(ResultStatus.Forbidden);
        }

        // Only the link goes; saved results stay (FR-014).
        var result = await _db.TournamentResults.FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        if (result is not null && result.TugenyTournamentId is not null)
        {
            result.TugenyTournamentId = null;
            result.TugenySlug = null;
            result.TugenyName = null;
            result.TugenyStartDate = null;
            await _db.SaveChangesAsync(ct);
        }

        return ResultOutcome<bool>.Ok(true);
    }

    public async Task<ResultOutcome<TugenyImportPreviewDto>> PreviewAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ev = await ResultEvents.LoadAsync(_db, eventId, userId, ct);
        if (ev is null)
        {
            return ResultOutcome<TugenyImportPreviewDto>.Fail(ResultStatus.NotFound);
        }

        if (ResultEvents.RecordGate(ev, now) is { } gate)
        {
            return ResultOutcome<TugenyImportPreviewDto>.Fail(gate);
        }

        var link = await LinkOfAsync(eventId, ct);
        if (link is null)
        {
            return ResultOutcome<TugenyImportPreviewDto>.Fail(ResultStatus.NotLinked);
        }

        var fetched = await FetchAsync(link.Value.TournamentId, ct);
        if (fetched.Failure is { } failure)
        {
            return ResultOutcome<TugenyImportPreviewDto>.Fail(failure);
        }

        var hasSaved = await _db.TournamentPlacements.AnyAsync(p => p.TournamentResult.EventId == eventId, ct);
        return ResultOutcome<TugenyImportPreviewDto>.Ok(new TugenyImportPreviewDto(
            link.Value.Name ?? string.Empty,
            fetched.Ranking.Select(t => new ImportPlacementDto(t.Position, t.TeamName, t.TeamId)).ToList(),
            fetched.Matches.Count,
            await ResultEvents.SignedUpTeamsAsync(_db, eventId, ct),
            hasSaved));
    }

    public async Task<ResultOutcome<TournamentResultDto>> CommitAsync(
        Guid eventId, Guid userId, IReadOnlyList<ImportConnectionRequest> connections, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ev = await ResultEvents.LoadAsync(_db, eventId, userId, ct);
        if (ev is null)
        {
            return ResultOutcome<TournamentResultDto>.Fail(ResultStatus.NotFound);
        }

        if (ResultEvents.RecordGate(ev, now) is { } gate)
        {
            return ResultOutcome<TournamentResultDto>.Fail(gate);
        }

        var link = await LinkOfAsync(eventId, ct);
        if (link is null)
        {
            return ResultOutcome<TournamentResultDto>.Fail(ResultStatus.NotLinked);
        }

        // Each connection goes to a different team with a confirmed JuggerHub sign-up (FR-004a, FR-005).
        var signedUp = (await ResultEvents.SignedUpTeamsAsync(_db, eventId, ct)).ToDictionary(t => t.TeamId, t => t.TeamName);
        if (connections.Any(c => !signedUp.ContainsKey(c.TeamId)))
        {
            return ResultOutcome<TournamentResultDto>.Fail(
                ResultStatus.TeamNotAllowed,
                "An event admin can only connect a placement to a team with a confirmed JuggerHub sign-up for this event.");
        }

        if (connections.Select(c => c.TeamId).Distinct().Count() != connections.Count
            || connections.Select(c => c.TugenyTeamId).Distinct().Count() != connections.Count)
        {
            return ResultOutcome<TournamentResultDto>.Fail(
                ResultStatus.TeamNotAllowed, "A team can hold only one placement in a ranking.");
        }

        var fetched = await FetchAsync(link.Value.TournamentId, ct);
        if (fetched.Failure is { } failure)
        {
            return ResultOutcome<TournamentResultDto>.Fail(failure);
        }

        var inRanking = fetched.Ranking.Select(t => t.TeamId).ToHashSet();
        if (connections.Any(c => !inRanking.Contains(c.TugenyTeamId)))
        {
            return ResultOutcome<TournamentResultDto>.Fail(ResultStatus.Invalid, "A connection names a team that is not in this Tugeny ranking.");
        }

        var chosen = connections.ToDictionary(c => c.TugenyTeamId, c => c.TeamId);

        // One retriable unit (Principle VII). Two saves inside it: the old rows go first, because the
        // unique (result, team) index is checked per statement and the new rows may reuse a team.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var result = await _db.TournamentResults
                .Include(r => r.Placements)
                .Include(r => r.Matches)
                .FirstAsync(r => r.EventId == eventId, ct);

            _db.TournamentMatches.RemoveRange(result.Matches);
            _db.TournamentPlacements.RemoveRange(result.Placements);
            await _db.SaveChangesAsync(ct);

            var byTugenyTeam = new Dictionary<int, TournamentPlacement>();
            for (var i = 0; i < fetched.Ranking.Count; i++)
            {
                var team = fetched.Ranking[i];
                var name = Truncate(team.TeamName, TournamentResultService.MaxNameLength);
                var placement = new TournamentPlacement
                {
                    TournamentResultId = result.Id,
                    Position = team.Position,
                    SortIndex = i,
                    SourceName = name,
                    Name = name,
                    TugenyTeamId = team.TeamId,
                };

                if (chosen.TryGetValue(team.TeamId, out var teamId))
                {
                    placement.TeamId = teamId;
                    placement.Name = signedUp[teamId];
                    placement.ConnectedByUserId = userId;
                    placement.ConnectedAt = now;
                }

                byTugenyTeam[team.TeamId] = placement;
                _db.TournamentPlacements.Add(placement);
            }

            // Tugeny's match list is not in time order; its timestamps ("yyyy-MM-dd HH:mm") sort as text.
            var ordered = fetched.Matches
                .OrderBy(m => m.Timestamp is null)
                .ThenBy(m => m.Timestamp, StringComparer.Ordinal)
                .ThenBy(m => m.ResponseIndex)
                .ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var m = ordered[i];
                _db.TournamentMatches.Add(new TournamentMatch
                {
                    TournamentResultId = result.Id,
                    SortIndex = i,
                    Stage = m.Group is null ? null : Truncate(m.Group, 80),
                    Name = Truncate(m.Name, 120),
                    FirstName = Truncate(m.FirstTeam, TournamentResultService.MaxNameLength),
                    SecondName = Truncate(m.SecondTeam, TournamentResultService.MaxNameLength),
                    FirstPlacementId = byTugenyTeam.GetValueOrDefault(m.FirstTeamId)?.Id,
                    SecondPlacementId = byTugenyTeam.GetValueOrDefault(m.SecondTeamId)?.Id,
                    FirstScores = m.FirstScores,
                    SecondScores = m.SecondScores,
                    Winner = m.WinnerTeamId == m.FirstTeamId ? MatchWinner.First
                        : m.WinnerTeamId == m.SecondTeamId ? MatchWinner.Second
                        : MatchWinner.Draw,
                });
            }

            result.Source = ResultSource.TugenyImport;
            result.ImportedAt = now;
            result.EditedSinceImport = false;
            result.ResultsChangedAt = now;
            result.LastChangedByUserId = userId;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return ResultOutcome<TournamentResultDto>.Ok((await _results.GetAsync(eventId, userId, ct))!);
    }

    /// <summary>Linking needs an admin of an uncancelled tournament; unlike recording, not a start.</summary>
    private static ResultStatus? LinkGate(ResultEvent? ev) =>
        ev is null ? ResultStatus.NotFound
        : !ev.IsAdmin ? ResultStatus.Forbidden
        : !ev.IsTournament ? ResultStatus.NotATournament
        : ev.IsCancelled ? ResultStatus.Cancelled
        : null;

    private async Task<(int TournamentId, string? Name)?> LinkOfAsync(Guid eventId, CancellationToken ct)
    {
        var link = await _db.TournamentResults.AsNoTracking()
            .Where(r => r.EventId == eventId && r.TugenyTournamentId != null)
            .Select(r => new { r.TugenyTournamentId, r.TugenyName })
            .FirstOrDefaultAsync(ct);
        return link is null ? null : (link.TugenyTournamentId!.Value, link.TugenyName);
    }

    /// <summary>Ranking and matches from Tugeny, or the reason they can't be imported.</summary>
    private async Task<(IReadOnlyList<TugenyRankedTeam> Ranking, IReadOnlyList<TugenyMatch> Matches, ResultStatus? Failure)> FetchAsync(
        int tournamentId, CancellationToken ct)
    {
        var ranking = await _tugeny.GetRankingAsync(tournamentId, ct);
        if (ranking.Outcome != TugenyOutcome.Ok)
        {
            return ([], [], ResultStatus.TugenyUnavailable);
        }

        if (ranking.Value!.Count == 0)
        {
            return ([], [], ResultStatus.TugenyNotFinalized);
        }

        var matches = await _tugeny.GetMatchesAsync(tournamentId, ct);
        return matches.Outcome != TugenyOutcome.Ok
            ? ([], [], ResultStatus.TugenyUnavailable)
            : (ranking.Value, matches.Value!, null);
    }

    private static string Truncate(string value, int max)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
