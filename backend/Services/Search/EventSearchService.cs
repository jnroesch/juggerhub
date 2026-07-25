using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Cities;
using JuggerHub.Dtos.Search;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Search;

/// <summary>Event browse/search (feature 007; proximity feature 030). Cancelled events are always excluded.</summary>
public interface IEventSearchService
{
    /// <remarks>
    /// <paramref name="homeCityId"/> is the caller's home city, required only for
    /// <see cref="EventSort.Proximity"/>; when null under that sort the service falls back to the
    /// default ordering (the controller returns 409 first).
    /// </remarks>
    Task<PagedResult<EventCardDto>> BrowseAsync(
        EventBrowseQuery query, PaginationRequest pagination, Guid? homeCityId = null, CancellationToken ct = default);
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
        EventBrowseQuery query, PaginationRequest pagination, Guid? homeCityId = null, CancellationToken ct = default)
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

        if (!string.IsNullOrWhiteSpace(query.Country))
        {
            var country = query.Country.Trim();
            q = q.Where(e => e.City != null
                && (e.City.CountryCode == country
                    || EF.Functions.ILike(AppDbContext.Unaccent(e.City.CountryName), AppDbContext.Unaccent(country))));
        }

        var term = SearchQuery.Normalize(query.Q, _options.MinQueryLength);
        if (term is not null)
        {
            var pattern = SearchQuery.ContainsPattern(term);
            q = q.Where(e =>
                EF.Functions.ILike(AppDbContext.Unaccent(e.Name), AppDbContext.Unaccent(pattern)));
        }

        var useProximity = query.Sort == EventSort.Proximity && homeCityId is not null;
        if (useProximity)
        {
            // Proximity view shows located events only — virtual/cityless events are excluded
            // entirely (feature 030, FR-016, clarified 2026-07-25).
            q = q.Where(e => e.CityId != null);
        }

        var total = await q.CountAsync(ct);

        var page = useProximity
            ? ProximityPage(q, homeCityId!.Value, pagination)
            : q.OrderBy(e => e.StartsAt).ThenBy(e => e.Id) // stable tiebreaker
                .Skip(pagination.NormalizedSkip).Take(pagination.NormalizedTake)
                .Select(EventCardProjection());

        var items = await page.ToListAsync(ct);
        return new PagedResult<EventCardDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    // Nearest-first via the CityDistance cache; only located events reach here (virtual excluded above).
    private IQueryable<EventCardDto> ProximityPage(
        IQueryable<Event> q, Guid homeCityId, PaginationRequest pagination)
    {
        var ordered =
            from e in q
            join d in _db.CityDistances.Where(cd => cd.FromCityId == homeCityId)
                on e.CityId equals (Guid?)d.ToCityId
            orderby d.DistanceKm, e.Id
            select e;

        return ordered
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(EventCardProjection());
    }

    private static System.Linq.Expressions.Expression<Func<Event, EventCardDto>> EventCardProjection() =>
        e => new EventCardDto(
            e.Id,
            e.Name,
            e.Type,
            e.CustomTypeLabel,
            e.StartsAt,
            e.EndsAt,
            e.LocationKind,
            e.City == null
                ? null
                : new LocationDto(
                    e.City.ExternalId, e.City.Name, e.City.Region, e.City.CountryName, e.City.CountryCode,
                    e.City.Name + ", " + e.City.CountryName),
            e.LocationKind == LocationKind.Virtual
                ? "Online"
                : (e.City == null ? string.Empty : e.City.Name + ", " + e.City.CountryName));
}
