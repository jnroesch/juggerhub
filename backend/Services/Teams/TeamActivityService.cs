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
            .Select(ep => new { ep.EventId, ep.Event.Name, ep.Event.Date, ep.Event.Location, ep.TeamLabel })
            .Distinct();

        var total = await events.CountAsync(ct);
        var items = await events
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.EventId)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(x => new ActivityItemDto(x.Name, x.Date, x.Location, x.TeamLabel))
            .ToListAsync(ct);

        return new PagedResult<ActivityItemDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }
}
