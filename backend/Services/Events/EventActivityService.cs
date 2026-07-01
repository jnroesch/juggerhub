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

        var items = await baseQuery
            .OrderByDescending(ep => ep.Event.Date)
            .ThenByDescending(ep => ep.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(ep => new ActivityItemDto(ep.Event.Name, ep.Event.Date, ep.Event.Location, ep.TeamLabel))
            .ToListAsync(ct);

        return new PagedResult<ActivityItemDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<IReadOnlyList<ActivityItemDto>> GetRecentCappedAsync(
        Guid profileId, int take, CancellationToken ct = default)
    {
        return await _db.EventParticipations
            .AsNoTracking()
            .Where(ep => ep.ProfileId == profileId)
            .OrderByDescending(ep => ep.Event.Date)
            .ThenByDescending(ep => ep.CreatedDate)
            .Take(take)
            .Select(ep => new ActivityItemDto(ep.Event.Name, ep.Event.Date, ep.Event.Location, ep.TeamLabel))
            .ToListAsync(ct);
    }
}
