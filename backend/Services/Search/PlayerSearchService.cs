using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Cities;
using JuggerHub.Dtos.Search;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Search;

/// <summary>
/// Anonymous player browse/search (feature 007). Returns every non-banned player matching the
/// query. Banned accounts are excluded globally by the <see cref="Entities.PlayerProfile"/> query
/// filter (feature 013); suspended accounts stay visible. The former per-player opt-in gate
/// (AppearInSearch) was removed in feature 020 — see specs/020-remove-search-optout.
/// </summary>
public interface IPlayerSearchService
{
    /// <remarks>
    /// <paramref name="homeCityId"/> is the caller's home city, required only for
    /// <see cref="PlayerSort.Proximity"/>; when null under that sort the service falls back to the
    /// default ordering (the controller returns 409 first).
    /// </remarks>
    Task<PagedResult<PlayerCardDto>> BrowseAsync(
        PlayerBrowseQuery query, PaginationRequest pagination, Guid? homeCityId = null, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class PlayerSearchService : IPlayerSearchService
{
    private readonly AppDbContext _db;
    private readonly SearchOptions _options;

    public PlayerSearchService(AppDbContext db, IOptions<SearchOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PagedResult<PlayerCardDto>> BrowseAsync(
        PlayerBrowseQuery query, PaginationRequest pagination, Guid? homeCityId = null, CancellationToken ct = default)
    {
        // All non-banned players are browseable (the AppearInSearch opt-in was removed in feature
        // 020). Banned accounts are still excluded by the global PlayerProfile query filter.
        var q = _db.PlayerProfiles.AsNoTracking();

        if (query.Positions is { Count: > 0 } positions)
        {
            q = q.Where(p => p.Pompfen.Any(pp => positions.Contains(pp.Pompfe)));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            q = q.Where(p => p.HomeCity != null
                && EF.Functions.ILike(AppDbContext.Unaccent(p.HomeCity.Name), AppDbContext.Unaccent(city)));
        }

        if (!string.IsNullOrWhiteSpace(query.Country))
        {
            var country = query.Country.Trim();
            q = q.Where(p => p.HomeCity != null
                && (p.HomeCity.CountryCode == country
                    || EF.Functions.ILike(AppDbContext.Unaccent(p.HomeCity.CountryName), AppDbContext.Unaccent(country))));
        }

        var term = SearchQuery.Normalize(query.Q, _options.MinQueryLength);
        if (term is not null)
        {
            var pattern = SearchQuery.ContainsPattern(term);
            q = q.Where(p =>
                EF.Functions.ILike(AppDbContext.Unaccent(p.DisplayName), AppDbContext.Unaccent(pattern)));
        }

        var total = await q.CountAsync(ct);

        var useProximity = query.Sort == PlayerSort.Proximity && homeCityId is not null;
        var page = useProximity
            ? ProximityPage(q, homeCityId!.Value, pagination)
            : q.OrderBy(p => p.DisplayName)
                .ThenBy(p => p.Id) // stable tiebreaker
                .Skip(pagination.NormalizedSkip).Take(pagination.NormalizedTake)
                .Select(PlayerCardProjection());

        var items = await page.ToListAsync(ct);

        // Proximity excludes players with no home city — the join drops them — so a proximity page
        // may be shorter than the unfiltered total. Report the count that matches the view, using
        // the join's own predicate (mirrors TeamSearchService; counting before the join, as the
        // events service still does, overstates the page).
        var effectiveTotal = useProximity
            ? await q.CountAsync(p => p.HomeCityId != null && _db.CityDistances.Any(
                d => d.FromCityId == homeCityId!.Value && d.ToCityId == p.HomeCityId), ct)
            : total;

        return new PagedResult<PlayerCardDto>(
            items, effectiveTotal, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    // Nearest-first via the CityDistance cache, anchored on the caller's home city. Players with no
    // home city are excluded (the join has no row); ties break on Id (feature 030, FR-011/FR-012).
    private IQueryable<PlayerCardDto> ProximityPage(
        IQueryable<PlayerProfile> q, Guid homeCityId, PaginationRequest pagination)
    {
        var ordered =
            from p in q
            join d in _db.CityDistances.Where(cd => cd.FromCityId == homeCityId)
                on p.HomeCityId equals (Guid?)d.ToCityId
            orderby d.DistanceKm, p.Id
            select p;

        return ordered
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(PlayerCardProjection());
    }

    private static System.Linq.Expressions.Expression<Func<PlayerProfile, PlayerCardDto>> PlayerCardProjection() =>
        p => new PlayerCardDto(
            p.Handle,
            p.DisplayName,
            p.HomeCity == null
                ? null
                : new LocationDto(
                    p.HomeCity.ExternalId, p.HomeCity.Name, p.HomeCity.Region, p.HomeCity.CountryName, p.HomeCity.CountryCode,
                    p.HomeCity.Name + ", " + p.HomeCity.CountryName),
            p.Pompfen.OrderBy(pp => pp.Pompfe).Select(pp => pp.Pompfe).ToList(),
            p.Avatar != null);
}
