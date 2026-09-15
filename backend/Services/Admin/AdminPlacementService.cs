using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Admin;
using JuggerHub.Services.Localization;
using JuggerHub.Services.Results;
using JuggerHub.Services.Search;
using JuggerHub.Services.Teams;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Admin;

/// <summary>
/// The platform admins' queue of tournament placements, and connecting each one to a team (feature
/// 050, US6).
/// </summary>
public interface IAdminPlacementService
{
    /// <summary>Placements, unconnected by default, newest tournament first.</summary>
    Task<PagedResult<AdminPlacementDto>> ListAsync(bool connected, string? q, PaginationRequest page, CancellationToken ct = default);

    Task<ResultOutcome<AdminPlacementDto>> ConnectAsync(Guid placementId, string teamSlug, Guid actorId, CancellationToken ct = default);

    Task<ResultOutcome<AdminPlacementDto>> DisconnectAsync(Guid placementId, CancellationToken ct = default);
}

/// <inheritdoc />
/// <remarks>
/// <para>
/// This is how teams that played without a JuggerHub sign-up — every team of a past tournament,
/// sign-ups run elsewhere, guests added on the day — get their placements: a platform admin checks
/// by hand that the team really played, then connects it. Teams never claim (owner decision).
/// </para>
/// <para>
/// <b>One placement at a time</b> (FR-025): connecting touches exactly one row. Nothing is applied to,
/// suggested for, or pre-filled on any other placement — not one with the same name, and not one from
/// the same Tugeny team in another import. The owner chose manual checking over bulk backfilling.
/// </para>
/// <para>
/// Trusts the controller's <c>PlatformAdmin</c> policy, like every other admin service.
/// </para>
/// </remarks>
public sealed class AdminPlacementService : IAdminPlacementService
{
    private readonly AppDbContext _db;
    private readonly IRecipientCultureResolver _culture;

    public AdminPlacementService(AppDbContext db, IRecipientCultureResolver culture)
    {
        _db = db;
        _culture = culture;
    }

    public async Task<PagedResult<AdminPlacementDto>> ListAsync(
        bool connected, string? q, PaginationRequest page, CancellationToken ct = default)
    {
        var query = _db.TournamentPlacements.AsNoTracking().Where(p => (p.TeamId != null) == connected);

        var term = SearchQuery.Normalize(q, minLength: 1);
        if (term is not null)
        {
            var pattern = SearchQuery.ContainsPattern(term);
            query = query.Where(p =>
                EF.Functions.ILike(AppDbContext.Unaccent(p.SourceName), AppDbContext.Unaccent(pattern))
                || EF.Functions.ILike(AppDbContext.Unaccent(p.Name), AppDbContext.Unaccent(pattern)));
        }

        var total = await query.CountAsync(ct);
        var ids = await query
            .OrderByDescending(p => p.TournamentResult.Event.StartsAt)
            .ThenBy(p => p.TournamentResult.EventId)
            .ThenBy(p => p.Position)
            .ThenBy(p => p.SortIndex)
            .Skip(page.NormalizedSkip)
            .Take(page.NormalizedTake)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var rows = await ProjectAsync(ids, ct);
        return new PagedResult<AdminPlacementDto>(rows, total, page.NormalizedSkip, page.NormalizedTake);
    }

    public async Task<ResultOutcome<AdminPlacementDto>> ConnectAsync(
        Guid placementId, string teamSlug, Guid actorId, CancellationToken ct = default)
    {
        var placement = await _db.TournamentPlacements.FirstOrDefaultAsync(p => p.Id == placementId, ct);
        if (placement is null)
        {
            return ResultOutcome<AdminPlacementDto>.Fail(ResultStatus.NotFound, "No placement matches that id.");
        }

        var normalized = TeamSlugPolicy.Normalize(teamSlug);
        var team = await _db.Teams.AsNoTracking()
            .Where(t => t.Slug == normalized)
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(ct);
        if (team is null)
        {
            return ResultOutcome<AdminPlacementDto>.Fail(ResultStatus.NotFound, "No team matches that slug.");
        }

        if (placement.TeamId != team.Id)
        {
            // A team holds one placement per ranking (FR-005); the unique index backs this check.
            var alreadyPlaced = await _db.TournamentPlacements.AnyAsync(
                p => p.TournamentResultId == placement.TournamentResultId && p.TeamId == team.Id && p.Id != placement.Id, ct);
            if (alreadyPlaced)
            {
                return ResultOutcome<AdminPlacementDto>.Fail(
                    ResultStatus.AlreadyPlaced, "That team already holds a placement in this ranking.");
            }

            var now = DateTime.UtcNow;
            placement.TeamId = team.Id;
            placement.Name = team.Name;
            placement.ConnectedByUserId = actorId;
            placement.ConnectedAt = now;
            await TouchResultAsync(placement.TournamentResultId, now, ct);
            await _db.SaveChangesAsync(ct);
        }

        return ResultOutcome<AdminPlacementDto>.Ok((await ProjectAsync([placement.Id], ct))[0]);
    }

    public async Task<ResultOutcome<AdminPlacementDto>> DisconnectAsync(Guid placementId, CancellationToken ct = default)
    {
        var placement = await _db.TournamentPlacements.FirstOrDefaultAsync(p => p.Id == placementId, ct);
        if (placement is null)
        {
            return ResultOutcome<AdminPlacementDto>.Fail(ResultStatus.NotFound, "No placement matches that id.");
        }

        if (placement.TeamId is not null)
        {
            var now = DateTime.UtcNow;
            placement.TeamId = null;
            placement.ConnectedByUserId = null;
            placement.ConnectedAt = null;
            placement.Name = placement.SourceName;
            await TouchResultAsync(placement.TournamentResultId, now, ct);
            await _db.SaveChangesAsync(ct);
        }

        return ResultOutcome<AdminPlacementDto>.Ok((await ProjectAsync([placement.Id], ct))[0]);
    }

    /// <summary>A connection changes what the event page shows, so it moves "last changed" too.</summary>
    private async Task TouchResultAsync(Guid resultId, DateTime now, CancellationToken ct)
    {
        var result = await _db.TournamentResults.FirstAsync(r => r.Id == resultId, ct);
        result.ResultsChangedAt = now;
    }

    private async Task<List<AdminPlacementDto>> ProjectAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var placeholder = MemberPlaceholder.For(_culture.ResolveFromRequest());
        var rows = await _db.TournamentPlacements.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.TournamentResult.EventId,
                EventName = p.TournamentResult.Event.Name,
                p.TournamentResult.Event.StartsAt,
                p.Position,
                Ranked = p.TournamentResult.Placements.Count,
                p.SourceName,
                p.Name,
                TeamSlug = p.Team != null ? p.Team.Slug : null,
                TeamName = p.Team != null ? p.Team.Name : null,
                // Through the profile set, never User.Profile (the ban filter); an erased or banned
                // connector reads as the neutral placeholder.
                ConnectedBy = p.ConnectedByUserId == null
                    ? null
                    : _db.PlayerProfiles.Where(pp => pp.UserId == p.ConnectedByUserId)
                        .Select(pp => pp.DisplayName).FirstOrDefault() ?? placeholder,
                p.ConnectedAt,
                FromTugeny = p.TugenyTeamId != null,
            })
            .ToListAsync(ct);

        // Keep the caller's order (the paged query's).
        var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return rows
            .OrderBy(r => order[r.Id])
            .Select(r => new AdminPlacementDto(
                r.Id,
                r.EventId,
                r.EventName,
                DateOnly.FromDateTime(r.StartsAt),
                r.Position,
                r.Ranked,
                r.SourceName,
                r.Name,
                r.TeamSlug is null ? null : new AdminPlacementTeamDto(r.TeamSlug, r.TeamName!),
                r.ConnectedBy,
                r.ConnectedAt,
                r.FromTugeny))
            .ToList();
    }
}
