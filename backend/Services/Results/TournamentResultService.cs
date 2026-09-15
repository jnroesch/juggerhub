using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Results;
using JuggerHub.Entities;
using JuggerHub.Services.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Results;

/// <summary>Reading and hand-writing a tournament's results (feature 050).</summary>
public interface ITournamentResultService
{
    /// <summary>The results section of an event; null when the event does not exist.</summary>
    Task<TournamentResultDto?> GetAsync(Guid eventId, Guid viewerId, CancellationToken ct = default);

    /// <summary>An event's imported matches, paged in import order; null when the event does not exist.</summary>
    Task<PagedResult<TournamentMatchDto>?> GetMatchesAsync(Guid eventId, PaginationRequest page, CancellationToken ct = default);

    /// <summary>Everything the results page needs, for an admin of the event.</summary>
    Task<ResultOutcome<ResultEditorDto>> GetEditorAsync(Guid eventId, Guid userId, CancellationToken ct = default);

    /// <summary>Replace the ranking (research R8).</summary>
    Task<ResultOutcome<TournamentResultDto>> ReplaceRankingAsync(
        Guid eventId, Guid userId, IReadOnlyList<RankingRowRequest> rows, CancellationToken ct = default);

    /// <summary>Remove the ranking and matches, keeping the row (and with it a Tugeny link).</summary>
    Task<ResultOutcome<bool>> ClearAsync(Guid eventId, Guid userId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class TournamentResultService : ITournamentResultService
{
    /// <summary>Most placements one ranking may hold. The largest real tournament has 72.</summary>
    public const int MaxPlacements = 128;

    /// <summary>Longest team name a placement stores (the same bound as a team snapshot elsewhere).</summary>
    public const int MaxNameLength = 80;

    private readonly AppDbContext _db;
    private readonly TugenyOptions _tugeny;
    private readonly IRecipientCultureResolver _culture;

    public TournamentResultService(AppDbContext db, IOptions<TugenyOptions> tugeny, IRecipientCultureResolver culture)
    {
        _db = db;
        _tugeny = tugeny.Value;
        _culture = culture;
    }

    public async Task<TournamentResultDto?> GetAsync(Guid eventId, Guid viewerId, CancellationToken ct = default)
    {
        var ev = await ResultEvents.LoadAsync(_db, eventId, viewerId, ct);
        return ev is null ? null : await BuildResultAsync(ev, DateTime.UtcNow, ct);
    }

    public async Task<PagedResult<TournamentMatchDto>?> GetMatchesAsync(
        Guid eventId, PaginationRequest page, CancellationToken ct = default)
    {
        if (!await _db.Events.AnyAsync(e => e.Id == eventId, ct))
        {
            return null;
        }

        var query = _db.TournamentMatches.AsNoTracking().Where(m => m.TournamentResult.EventId == eventId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.SortIndex)
            .Skip(page.NormalizedSkip)
            .Take(page.NormalizedTake)
            .Select(m => new TournamentMatchDto(
                m.Id,
                m.Stage,
                m.Name,
                // A side shows its placement's name — the team's, once connected — like the ranking.
                new MatchSideDto(
                    m.FirstPlacement != null ? m.FirstPlacement.Name : m.FirstName,
                    m.FirstPlacement != null && m.FirstPlacement.Team != null ? m.FirstPlacement.Team.Slug : null),
                new MatchSideDto(
                    m.SecondPlacement != null ? m.SecondPlacement.Name : m.SecondName,
                    m.SecondPlacement != null && m.SecondPlacement.Team != null ? m.SecondPlacement.Team.Slug : null),
                m.FirstScores,
                m.SecondScores,
                m.Winner))
            .ToListAsync(ct);

        return new PagedResult<TournamentMatchDto>(items, total, page.NormalizedSkip, page.NormalizedTake);
    }

    public async Task<ResultOutcome<ResultEditorDto>> GetEditorAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var ev = await ResultEvents.LoadAsync(_db, eventId, userId, ct);
        if (ev is null)
        {
            return ResultOutcome<ResultEditorDto>.Fail(ResultStatus.NotFound);
        }

        if (!ev.IsAdmin)
        {
            return ResultOutcome<ResultEditorDto>.Fail(ResultStatus.Forbidden);
        }

        return ResultOutcome<ResultEditorDto>.Ok(await BuildEditorAsync(ev, DateTime.UtcNow, ct));
    }

    public async Task<ResultOutcome<TournamentResultDto>> ReplaceRankingAsync(
        Guid eventId, Guid userId, IReadOnlyList<RankingRowRequest> rows, CancellationToken ct = default)
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

        if (Validate(rows) is { } invalid)
        {
            return ResultOutcome<TournamentResultDto>.Fail(ResultStatus.Invalid, invalid.Detail, invalid.Row);
        }

        var ranked = TournamentRanking.Normalize(rows.Select(r => r.Position).ToList());
        var requestedTeams = rows.Where(r => r.TeamId is not null).Select(r => r.TeamId!.Value).ToList();
        var teamNames = await _db.Teams.AsNoTracking()
            .Where(t => requestedTeams.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var signedUp = (await ResultEvents.SignedUpTeamsAsync(_db, eventId, ct)).Select(t => t.TeamId).ToHashSet();

        // One retriable unit (Principle VII): the connection rules are checked against the rows as they
        // are inside the transaction, and everything that mutates state lives in the delegate.
        var strategy = _db.Database.CreateExecutionStrategy();
        var failure = await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var result = await _db.TournamentResults
                .Include(r => r.Placements)
                .FirstOrDefaultAsync(r => r.EventId == eventId, ct);
            if (result is null)
            {
                result = new TournamentResult { EventId = eventId };
                _db.TournamentResults.Add(result);
            }

            var existing = result.Placements.ToDictionary(p => p.Id);
            var teamBefore = existing.ToDictionary(kv => kv.Key, kv => kv.Value.TeamId);

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Id is { } id && !existing.ContainsKey(id))
                {
                    return ResultOutcome<TournamentResultDto>.Fail(
                        ResultStatus.Invalid, "A row refers to a placement that is not part of this ranking.", i);
                }

                if (row.TeamId is not { } teamId)
                {
                    continue;
                }

                // Research R8: an event admin keeps an existing connection on its own row — that is how
                // a platform admin's connection survives the edit — but creates or moves one only to a
                // team with a confirmed JuggerHub sign-up for this event (FR-004a, FR-027).
                var unchanged = row.Id is { } keptId && teamBefore[keptId] == teamId;
                if ((!unchanged && !signedUp.Contains(teamId)) || !teamNames.ContainsKey(teamId))
                {
                    return ResultOutcome<TournamentResultDto>.Fail(
                        ResultStatus.TeamNotAllowed,
                        "An event admin can only connect a placement to a team with a confirmed JuggerHub "
                        + "sign-up for this event. Any other team is connected by a platform admin.",
                        i);
                }
            }

            // Phase 1 — release every team whose placement goes or changes. The unique (result, team)
            // index is checked per statement, so moving a team between two rows in one save would
            // collide with itself without this.
            var kept = rows.Where(r => r.Id is not null).Select(r => r.Id!.Value).ToHashSet();
            var removed = result.Placements.Where(p => !kept.Contains(p.Id)).ToList();
            _db.TournamentPlacements.RemoveRange(removed);
            var moving = rows
                .Where(r => r.Id is { } id && teamBefore[id] is not null && teamBefore[id] != r.TeamId)
                .Select(r => existing[r.Id!.Value])
                .ToList();
            foreach (var placement in moving)
            {
                placement.TeamId = null;
            }

            if (removed.Count > 0 || moving.Count > 0)
            {
                await _db.SaveChangesAsync(ct);
            }

            // Phase 2 — write the ranking.
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var (position, sortIndex) = ranked[i];
                var name = row.Name.Trim();

                if (row.Id is { } id)
                {
                    var placement = existing[id];
                    placement.Position = position;
                    placement.SortIndex = sortIndex;
                    Apply(placement, row.TeamId, teamBefore[id], name, teamNames, userId, now);
                }
                else
                {
                    var placement = new TournamentPlacement
                    {
                        TournamentResultId = result.Id,
                        Position = position,
                        SortIndex = sortIndex,
                        SourceName = name,
                        Name = name,
                    };
                    Apply(placement, row.TeamId, null, name, teamNames, userId, now);
                    // DbSet.Add, not the navigation: a client-generated key added through a collection
                    // navigation is misread as an existing row (EF gotcha).
                    _db.TournamentPlacements.Add(placement);
                }
            }

            if (result.Source == ResultSource.TugenyImport)
            {
                result.EditedSinceImport = true;
            }
            else
            {
                result.Source = ResultSource.Manual;
            }

            result.ResultsChangedAt = now;
            result.LastChangedByUserId = userId;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return null;
        });

        if (failure is not null)
        {
            return failure;
        }

        return ResultOutcome<TournamentResultDto>.Ok(await BuildResultAsync(ev, now, ct));
    }

    public async Task<ResultOutcome<bool>> ClearAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var ev = await ResultEvents.LoadAsync(_db, eventId, userId, ct);
        if (ev is null)
        {
            return ResultOutcome<bool>.Fail(ResultStatus.NotFound);
        }

        if (ResultEvents.RecordGate(ev, DateTime.UtcNow) is { } gate)
        {
            return ResultOutcome<bool>.Fail(gate);
        }

        var result = await _db.TournamentResults
            .Include(r => r.Placements)
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        if (result is not null)
        {
            // The row stays, so a Tugeny link survives a clear (FR-014). One SaveChanges: atomic.
            _db.TournamentMatches.RemoveRange(result.Matches);
            _db.TournamentPlacements.RemoveRange(result.Placements);
            result.Source = ResultSource.None;
            result.ImportedAt = null;
            result.EditedSinceImport = false;
            result.ResultsChangedAt = null;
            result.LastChangedByUserId = null;
            await _db.SaveChangesAsync(ct);
        }

        return ResultOutcome<bool>.Ok(true);
    }

    /// <summary>
    /// Sets a placement's team. An unchanged connection keeps its attribution (research R8); a new
    /// one is attributed to <paramref name="userId"/>; an unconnected row shows exactly what was
    /// typed — the retyped name becomes its <see cref="TournamentPlacement.SourceName"/> too.
    /// </summary>
    private static void Apply(
        TournamentPlacement placement, Guid? teamId, Guid? teamBefore, string typedName,
        IReadOnlyDictionary<Guid, string> teamNames, Guid userId, DateTime now)
    {
        if (teamId is { } team)
        {
            placement.TeamId = team;
            placement.Name = teamNames[team];
            if (teamBefore != team)
            {
                placement.ConnectedByUserId = userId;
                placement.ConnectedAt = now;
            }

            return;
        }

        placement.TeamId = null;
        placement.ConnectedByUserId = null;
        placement.ConnectedAt = null;
        placement.SourceName = typedName;
        placement.Name = typedName;
    }

    /// <summary>The first reason a ranking request is malformed, or null when it is well formed.</summary>
    private static (string Detail, int? Row)? Validate(IReadOnlyList<RankingRowRequest> rows)
    {
        if (rows.Count == 0)
        {
            return ("A ranking needs at least one placement. To remove it, clear the results.", null);
        }

        if (rows.Count > MaxPlacements)
        {
            return ($"A ranking can hold at most {MaxPlacements} placements.", null);
        }

        var ids = new HashSet<Guid>();
        var teams = new HashSet<Guid>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var name = row.Name?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                return ("Every placement needs a team name.", i);
            }

            if (name.Length > MaxNameLength)
            {
                return ($"A team name can be at most {MaxNameLength} characters.", i);
            }

            if (row.Position is < 1 or > 999)
            {
                return ("A placement must be between 1 and 999.", i);
            }

            if (row.Id is { } id && !ids.Add(id))
            {
                return ("The same placement appears twice.", i);
            }

            if (row.TeamId is { } team && !teams.Add(team))
            {
                return ("A team can hold only one placement in a ranking.", i);
            }
        }

        return null;
    }

    private async Task<TournamentResultDto> BuildResultAsync(ResultEvent ev, DateTime now, CancellationToken ct)
    {
        var result = await _db.TournamentResults.AsNoTracking()
            .Where(r => r.EventId == ev.Id)
            .Select(r => new
            {
                r.Source,
                r.ImportedAt,
                r.EditedSinceImport,
                r.ResultsChangedAt,
                r.TugenyTournamentId,
                r.TugenySlug,
                r.TugenyName,
                r.TugenyStartDate,
                MatchCount = r.Matches.Count,
                Placements = r.Placements
                    .OrderBy(p => p.Position).ThenBy(p => p.SortIndex)
                    .Select(p => new PlacementDto(p.Id, p.Position, p.Name, p.Team != null ? p.Team.Slug : null))
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        var viewer = new ResultViewerDto(ev.CanRecord(now));
        if (result is null)
        {
            return new TournamentResultDto(ResultSource.None, null, false, null, null, [], 0, 0, viewer);
        }

        return new TournamentResultDto(
            result.Source,
            result.ImportedAt,
            result.EditedSinceImport,
            result.ResultsChangedAt,
            TugenyLinks.Build(_tugeny, result.TugenyTournamentId, result.TugenySlug, result.TugenyName, result.TugenyStartDate),
            result.Placements,
            result.Placements.Count,
            result.MatchCount,
            viewer);
    }

    internal async Task<ResultEditorDto> BuildEditorAsync(ResultEvent ev, DateTime now, CancellationToken ct)
    {
        var placeholder = MemberPlaceholder.For(_culture.ResolveFromRequest());
        var result = await _db.TournamentResults.AsNoTracking()
            .Where(r => r.EventId == ev.Id)
            .Select(r => new
            {
                r.Source,
                r.ImportedAt,
                r.EditedSinceImport,
                r.ResultsChangedAt,
                r.TugenyTournamentId,
                r.TugenySlug,
                r.TugenyName,
                r.TugenyStartDate,
                LinkedElsewhere = r.TugenyTournamentId != null
                    && _db.TournamentResults.Any(o => o.TugenyTournamentId == r.TugenyTournamentId && o.EventId != r.EventId),
                Placements = r.Placements
                    .OrderBy(p => p.Position).ThenBy(p => p.SortIndex)
                    .Select(p => new EditorPlacementDto(
                        p.Id,
                        p.Position,
                        p.Name,
                        p.SourceName,
                        p.TeamId,
                        p.Team != null ? p.Team.Slug : null,
                        // Read the connector's name through the profile set, never the User.Profile
                        // navigation, which misbehaves against the profile ban filter; an erased or
                        // banned connector reads as the neutral placeholder.
                        p.ConnectedByUserId == null
                            ? null
                            : _db.PlayerProfiles.Where(pp => pp.UserId == p.ConnectedByUserId)
                                .Select(pp => pp.DisplayName).FirstOrDefault() ?? placeholder,
                        p.ConnectedAt))
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        var signedUp = await ResultEvents.SignedUpTeamsAsync(_db, ev.Id, ct);

        return new ResultEditorDto(
            ev.Id,
            ev.Name,
            ev.Mode,
            ev.IsTournament,
            ev.IsCancelled,
            ev.HasStarted(now),
            ev.HasEnded(now),
            ev.CanRecord(now),
            result?.Source ?? ResultSource.None,
            result?.ImportedAt,
            result?.EditedSinceImport ?? false,
            result?.ResultsChangedAt,
            result is null
                ? null
                : TugenyLinks.Build(_tugeny, result.TugenyTournamentId, result.TugenySlug, result.TugenyName, result.TugenyStartDate),
            result?.LinkedElsewhere ?? false,
            result?.Placements ?? [],
            signedUp);
    }
}
