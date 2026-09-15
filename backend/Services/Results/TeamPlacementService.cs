using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Results;
using JuggerHub.Services.Teams;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Results;

/// <summary>A team's tournament placement history (feature 050, US5).</summary>
public interface ITeamPlacementService
{
    /// <summary>The team's connected placements, newest tournament first; null for an unknown team.</summary>
    Task<PagedResult<TeamPlacementDto>?> GetForTeamAsync(string slug, PaginationRequest page, CancellationToken ct = default);
}

/// <inheritdoc />
/// <remarks>
/// Only connected placements count (FR-022, FR-026): a plain name belongs to no team, and a team's
/// history is only ever written by its own confirmed entry or a platform admin's check. Visible to
/// the same audience as the team page itself — every signed-in player (feature 026).
/// </remarks>
public sealed class TeamPlacementService : ITeamPlacementService
{
    private readonly AppDbContext _db;

    public TeamPlacementService(AppDbContext db) => _db = db;

    public async Task<PagedResult<TeamPlacementDto>?> GetForTeamAsync(
        string slug, PaginationRequest page, CancellationToken ct = default)
    {
        var normalized = TeamSlugPolicy.Normalize(slug);
        var teamId = await _db.Teams.AsNoTracking()
            .Where(t => t.Slug == normalized)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (teamId is null)
        {
            return null;
        }

        var query = _db.TournamentPlacements.AsNoTracking().Where(p => p.TeamId == teamId);
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(p => p.TournamentResult.Event.StartsAt)
            .ThenBy(p => p.TournamentResult.EventId)
            .Skip(page.NormalizedSkip)
            .Take(page.NormalizedTake)
            .Select(p => new
            {
                p.TournamentResult.EventId,
                p.TournamentResult.Event.Name,
                p.TournamentResult.Event.StartsAt,
                p.Position,
                Ranked = p.TournamentResult.Placements.Count,
            })
            .ToListAsync(ct);

        var items = rows
            .Select(r => new TeamPlacementDto(r.EventId, r.Name, DateOnly.FromDateTime(r.StartsAt), r.Position, r.Ranked))
            .ToList();
        return new PagedResult<TeamPlacementDto>(items, total, page.NormalizedSkip, page.NormalizedTake);
    }
}
