using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Search;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Search;

/// <summary>Anonymous event browse/search (feature 007). Cancelled events are always excluded.</summary>
public interface IEventSearchService
{
    Task<PagedResult<EventCardDto>> BrowseAsync(
        EventBrowseQuery query, PaginationRequest pagination, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class EventSearchService : IEventSearchService
{
    private readonly AppDbContext _db;
    private readonly SearchOptions _options;

    public EventSearchService(AppDbContext db, IOptions<SearchOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PagedResult<EventCardDto>> BrowseAsync(
        EventBrowseQuery query, PaginationRequest pagination, CancellationToken ct = default)
    {
        // Browse never surfaces cancelled events, regardless of any toggle (contract invariant).
        var q = _db.Events.AsNoTracking().Where(e => e.Status != EventStatus.Cancelled);

        if (query.HidePast)
        {
            var now = DateTime.UtcNow;
            q = q.Where(e => e.EndsAt >= now);
        }

        if (query.From is { } from)
        {
            var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            q = q.Where(e => e.StartsAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = DateTime.SpecifyKind(to.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
            q = q.Where(e => e.StartsAt <= toUtc);
        }

        if (query.Type is { } type)
        {
            q = q.Where(e => e.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            q = q.Where(e => e.City != null
                && EF.Functions.ILike(AppDbContext.Unaccent(e.City), AppDbContext.Unaccent(city)));
        }

        var term = SearchQuery.Normalize(query.Q, _options.MinQueryLength);
        if (term is not null)
        {
            var pattern = SearchQuery.ContainsPattern(term);
            q = q.Where(e =>
                EF.Functions.ILike(AppDbContext.Unaccent(e.Name), AppDbContext.Unaccent(pattern)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(e => e.StartsAt)
            .ThenBy(e => e.Id) // stable tiebreaker
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(e => new EventCardDto(
                e.Id,
                e.Name,
                e.Type,
                e.CustomTypeLabel,
                e.StartsAt,
                e.EndsAt,
                e.LocationKind,
                e.City,
                e.LocationKind == LocationKind.Virtual ? "Online" : (e.City ?? string.Empty)))
            .ToListAsync(ct);

        return new PagedResult<EventCardDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }
}
