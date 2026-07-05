using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Profile;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Events;

/// <inheritdoc />
public sealed class EventActivityService : IEventActivityService
{
    private readonly AppDbContext _db;

    public EventActivityService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ActivityItemDto>> GetRecentAsync(
        Guid profileId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var baseQuery = _db.EventParticipations
            .AsNoTracking()
            .Where(ep => ep.ProfileId == profileId);

        var total = await baseQuery.CountAsync(ct);

        // Project StartsAt (DateTime) and convert to the DTO's DateOnly in memory — avoids
        // relying on EF translating DateOnly.FromDateTime (feature 006 research §1).
        var rows = await baseQuery
            .OrderByDescending(ep => ep.Event.StartsAt)
            .ThenByDescending(ep => ep.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(ep => new { ep.Event.Name, ep.Event.StartsAt, ep.Event.Location, ep.TeamLabel })
            .ToListAsync(ct);

        var items = rows
            .Select(r => new ActivityItemDto(r.Name, DateOnly.FromDateTime(r.StartsAt), r.Location, r.TeamLabel))
            .ToList();

        return new PagedResult<ActivityItemDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<IReadOnlyList<ActivityItemDto>> GetRecentCappedAsync(
        Guid profileId, int take, CancellationToken ct = default)
    {
        var rows = await _db.EventParticipations
            .AsNoTracking()
            .Where(ep => ep.ProfileId == profileId)
            .OrderByDescending(ep => ep.Event.StartsAt)
            .ThenByDescending(ep => ep.CreatedDate)
            .Take(take)
            .Select(ep => new { ep.Event.Name, ep.Event.StartsAt, ep.Event.Location, ep.TeamLabel })
            .ToListAsync(ct);

        return rows
            .Select(r => new ActivityItemDto(r.Name, DateOnly.FromDateTime(r.StartsAt), r.Location, r.TeamLabel))
            .ToList();
    }
}
