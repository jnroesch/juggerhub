using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Profile;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Teams;

/// <summary>EF-Core-direct implementation of <see cref="ITeamActivityService"/>.</summary>
public sealed class TeamActivityService : ITeamActivityService
{
    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;

    public TeamActivityService(AppDbContext db, TeamMembershipGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    public async Task<PagedResult<ActivityItemDto>?> GetForTeamAsync(
        string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return null;
        }

        // Distinct events the team played (many members may share one event/participation).
        var events = _db.EventParticipations.AsNoTracking()
            .Where(ep => ep.TeamId == a.TeamId)
            .Select(ep => new { ep.EventId, ep.Event.Name, ep.Event.StartsAt, ep.Event.Location, ep.TeamLabel })
            .Distinct();

        var total = await events.CountAsync(ct);
        var rows = await events
            .OrderByDescending(x => x.StartsAt)
            .ThenBy(x => x.EventId)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .ToListAsync(ct);

        // Convert StartsAt (DateTime) → the DTO's DateOnly in memory (feature 006 research §1).
        var items = rows
            .Select(x => new ActivityItemDto(x.Name, DateOnly.FromDateTime(x.StartsAt), x.Location, x.TeamLabel))
            .ToList();

        return new PagedResult<ActivityItemDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }
}
