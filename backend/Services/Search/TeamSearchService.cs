using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Cities;
using JuggerHub.Dtos.Search;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Search;

/// <summary>Team browse/search (feature 007; proximity feature 030). Public card fields only.</summary>
public interface ITeamSearchService
{
    /// <remarks>
    /// <paramref name="homeCityId"/> is the caller's home city, required only for
    /// <see cref="TeamSort.Proximity"/>; when null under that sort the service falls back to the
    /// default ordering (the controller returns 409 first).
    /// </remarks>
    Task<PagedResult<TeamCardDto>> BrowseAsync(
        TeamBrowseQuery query, PaginationRequest pagination, Guid? homeCityId = null, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class TeamSearchService : ITeamSearchService
{
    private readonly AppDbContext _db;
    private readonly SearchOptions _options;

    public TeamSearchService(AppDbContext db, IOptions<SearchOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PagedResult<TeamCardDto>> BrowseAsync(
        TeamBrowseQuery query, PaginationRequest pagination, Guid? homeCityId = null, CancellationToken ct = default)
    {
        var q = _db.Teams.AsNoTracking();

        if (query.ActiveOnly)
        {
            // Active = created within the window OR participated in an event whose start is within
            // it. Newly-created teams count as active even before their first event (feature 008).
            var cutoff = DateTime.UtcNow.AddMonths(-_options.ActiveTeamWindowMonths);
            q = q.Where(t => t.CreatedDate >= cutoff
                || _db.EventParticipations.Any(ep => ep.TeamId == t.Id && ep.Event.StartsAt >= cutoff));
        }

        if (query.BeginnersWelcome)
        {
            q = q.Where(t => t.BeginnersWelcome);
        }

        if (!string.IsNullOrWhiteSpace(query.Country))
        {
            var country = query.Country.Trim();
            q = q.Where(t => t.City != null
                && (t.City.CountryCode == country
                    || EF.Functions.ILike(AppDbContext.Unaccent(t.City.CountryName), AppDbContext.Unaccent(country))));
        }

        var term = SearchQuery.Normalize(query.Q, _options.MinQueryLength);
        if (term is not null)
        {
            var pattern = SearchQuery.ContainsPattern(term);
            q = q.Where(t =>
                EF.Functions.ILike(AppDbContext.Unaccent(t.Name), AppDbContext.Unaccent(pattern))
                || (t.City != null
                    && EF.Functions.ILike(AppDbContext.Unaccent(t.City.Name), AppDbContext.Unaccent(pattern))));
        }

        var total = await q.CountAsync(ct);

        var useProximity = query.Sort == TeamSort.Proximity && homeCityId is not null;
        var page = useProximity
            ? ProximityPage(q, homeCityId!.Value, pagination)
            : q.OrderBy(t => t.Name).ThenBy(t => t.Id) // stable tiebreaker (research §5)
                .Skip(pagination.NormalizedSkip).Take(pagination.NormalizedTake)
                .Select(TeamCardProjection());

        var items = (await page.ToListAsync(ct))
            .Select(r => r with { LogoInitial = LogoInitial(r.Name) })
            .ToList();

        // Proximity excludes cityless teams (Mixteams) — the join drops them — so a proximity page
        // may be shorter than the unfiltered total. Report the count that matches the view.
        var effectiveTotal = useProximity
            ? await q.CountAsync(t => t.CityId != null && _db.CityDistances.Any(
                d => d.FromCityId == homeCityId!.Value && d.ToCityId == t.CityId), ct)
            : total;

        return new PagedResult<TeamCardDto>(
            items, effectiveTotal, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    // Nearest-first via the CityDistance cache, anchored on the caller's home city. Cityless teams
    // are excluded (the join has no row); ties break on Id (feature 030, FR-011/FR-012/FR-016).
    private IQueryable<TeamCardDto> ProximityPage(
        IQueryable<Team> q, Guid homeCityId, PaginationRequest pagination)
    {
        var ordered =
            from t in q
            join d in _db.CityDistances.Where(cd => cd.FromCityId == homeCityId)
                on t.CityId equals (Guid?)d.ToCityId
            orderby d.DistanceKm, t.Id
            select t;

        return ordered
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(TeamCardProjection());
    }

    private static System.Linq.Expressions.Expression<Func<Team, TeamCardDto>> TeamCardProjection() =>
        t => new TeamCardDto(
            t.Slug, t.Name,
            t.City == null
                ? null
                : new LocationDto(
                    t.City.ExternalId, t.City.Name, t.City.Region, t.City.CountryName, t.City.CountryCode,
                    t.City.Name + ", " + t.City.CountryName),
            t.Memberships.Count, t.BeginnersWelcome, string.Empty);

    private static string LogoInitial(string name)
    {
        var trimmed = name.TrimStart();
        return trimmed.Length == 0 ? "?" : trimmed[..1].ToUpperInvariant();
    }
}

