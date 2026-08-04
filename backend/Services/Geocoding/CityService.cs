using JuggerHub.Data;
using JuggerHub.Dtos.Cities;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Geocoding;

/// <inheritdoc />
/// <remarks>
/// Feature 030 research R8: the search + resolve source is the bundled, seeded <c>CityReference</c>
/// table (GeoNames cities500) — a local SQL query, not an external geocoder. Only <em>selected</em>
/// cities are copied into <see cref="City"/> (with the distance-cache backfill), which is what keeps
/// <see cref="CityDistance"/> small even though the reference table holds ~225k rows.
/// </remarks>
public sealed class CityService : ICityService
{
    // Mean Earth radius (km) — WGS84 authalic radius, good to well under the city-granularity
    // precision "near you" needs.
    private const double EarthRadiusKm = 6371.0088;

    private readonly AppDbContext _db;

    public CityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CityOptionDto>> SearchAsync(
        string query, int limit, Guid? userId, CancellationToken ct = default)
    {
        var term = query.Trim();
        if (term.Length == 0)
        {
            return Array.Empty<CityOptionDto>();
        }

        // Proximity origin (feature 032): the signed-in user's stored home city, resolved server-side.
        // Absent (no user, or no home city yet — e.g. onboarding) ⇒ distance ordering is skipped and
        // ranking falls back to population then name. Coordinates are never taken from the client.
        (double Lat, double Lon)? home = null;
        if (userId is Guid uid)
        {
            var coords = await _db.PlayerProfiles.AsNoTracking()
                .Where(p => p.UserId == uid && p.HomeCity != null)
                .Select(p => new { p.HomeCity!.Latitude, p.HomeCity.Longitude })
                .FirstOrDefaultAsync(ct);
            if (coords is not null)
            {
                home = (coords.Latitude, coords.Longitude);
            }
        }

        // Accent-insensitive prefix search over the canonical + ascii names, plus a token-prefix match
        // into the Latin alternate names so English exonyms resolve ("Muni" → München, "Colog" → Köln).
        // Unaccent is applied INSIDE the query on both column and pattern (it is a DB function, never
        // callable in C#); the raw patterns carry the literal wildcards.
        var prefix = term + "%";
        var altToken = "%," + term + "%";

        // Relevance order (feature 032): primary-name prefix hits rank above exonym/alternate hits (the
        // match tier), then — if a home city is known — nearer cities, then the more populous city of a
        // shared name, then the existing shortest-name/name tiebreakers for stability. Ordering runs
        // over the full prefix-filtered set here, before Take(limit), so the cap never drops a more
        // relevant city.
        IOrderedQueryable<Entities.CityReference> ordered = _db.CityReferences.AsNoTracking()
            .Where(r =>
                EF.Functions.ILike(AppDbContext.Unaccent(r.AsciiName), AppDbContext.Unaccent(prefix))
                || EF.Functions.ILike(AppDbContext.Unaccent(r.Name), AppDbContext.Unaccent(prefix))
                || EF.Functions.ILike(AppDbContext.Unaccent(r.AlternateNames), AppDbContext.Unaccent(prefix))
                || EF.Functions.ILike(AppDbContext.Unaccent(r.AlternateNames), AppDbContext.Unaccent(altToken)))
            .OrderByDescending(r => EF.Functions.ILike(AppDbContext.Unaccent(r.AsciiName), AppDbContext.Unaccent(prefix)));

        if (home is (double lat0, double lon0))
        {
            // Equirectangular squared distance: (Δlat)² + (cos(lat0)·Δlon)². Pure arithmetic on the
            // lat/lon columns, so EF translates it to SQL (no PostGIS, no trig, no C# haversine over the
            // candidate set). Squared, unscaled degrees are monotonic with true distance for the
            // city-granularity ranking this needs; cos(lat0) corrects longitude convergence.
            var lonScaleSq = Math.Cos(lat0 * Math.PI / 180.0);
            lonScaleSq *= lonScaleSq;
            ordered = ordered.ThenBy(r =>
                ((r.Latitude - lat0) * (r.Latitude - lat0))
                + (lonScaleSq * (r.Longitude - lon0) * (r.Longitude - lon0)));
        }

        var rows = await ordered
            .ThenByDescending(r => r.Population)
            .ThenBy(r => r.Name.Length)
            .ThenBy(r => r.Name)
            .Take(limit)
            .Select(r => new
            {
                r.ExternalId, r.Name, r.Region, r.CountryName, r.CountryCode, r.Latitude, r.Longitude,
            })
            .ToListAsync(ct);

        // Region is only shown when it actually disambiguates — i.e. more than one result shares the
        // same city name AND country (e.g. several "Berlin, United States"). Otherwise "City, Country"
        // is enough and cleaner (FR-003).
        var ambiguous = rows
            .GroupBy(r => (r.Name, r.CountryName))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        return rows.Select(r => new CityOptionDto(
            r.ExternalId,
            r.Name,
            string.IsNullOrEmpty(r.Region) ? null : r.Region,
            r.CountryName,
            string.IsNullOrEmpty(r.CountryCode) ? null : r.CountryCode,
            LocationLabels.Option(
                r.Name,
                string.IsNullOrEmpty(r.Region) ? null : r.Region,
                r.CountryName,
                ambiguous.Contains((r.Name, r.CountryName))),
            r.Latitude,
            r.Longitude)).ToList();
    }

    public async Task<IReadOnlyList<CountryDto>> ListCountriesAsync(CancellationToken ct = default)
    {
        // Distinct over the full reference dataset so EVERY country is offered — a viewer who filters
        // to a country with no teams/events yet simply gets the results' empty state, which is clearer
        // than silently omitting the country from the picker. One-off per session (client-cached), so
        // the distinct scan over the reference table is fine. CountryCode may be absent for a few rows.
        return await _db.CityReferences.AsNoTracking()
            .Select(c => new { c.CountryName, c.CountryCode })
            .Distinct()
            .OrderBy(c => c.CountryName)
            .Select(c => new CountryDto(
                string.IsNullOrEmpty(c.CountryCode) ? null : c.CountryCode,
                c.CountryName))
            .ToListAsync(ct);
    }

    public async Task<City> ResolveAndUpsertAsync(
        string externalId, string? nameHint, CancellationToken ct = default)
    {
        // 1) Reuse a city we already hold — no reference lookup, no re-backfill (FR-022).
        var existing = await _db.Cities.FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);
        if (existing is not null)
        {
            return existing;
        }

        // 2) First use: resolve authoritatively from the bundled reference — never from client-supplied
        // fields (Principle I). `nameHint` is ignored; the reference row is the source of truth.
        var reference = await _db.CityReferences.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ExternalId == externalId, ct)
            ?? throw new CityNotResolvableException(externalId);

        var city = new City
        {
            ExternalId = reference.ExternalId,
            Name = reference.Name,
            CountryName = reference.CountryName,
            CountryCode = string.IsNullOrEmpty(reference.CountryCode) ? null : reference.CountryCode,
            Region = string.IsNullOrEmpty(reference.Region) ? null : reference.Region,
            Latitude = reference.Latitude,
            Longitude = reference.Longitude,
        };

        _db.Cities.Add(city);
        var distances = AddDistanceRows(city, await LoadOtherCityPointsAsync(ct));

        // A single SaveChangesAsync is atomic and already runs through the provider's execution
        // strategy (EnableRetryOnFailure) — no manual transaction is opened, so the multi-step
        // execution-strategy dance (constitution VII) is not required here. The client-generated
        // UUIDv7 key makes a commit-time replay collide on the known id rather than duplicate.
        try
        {
            await _db.SaveChangesAsync(ct);
            return city;
        }
        catch (DbUpdateException)
        {
            // Lost the create race: another request inserted this ExternalId first (unique index).
            // Discard our losing insert (city + its distance rows) and return the winner.
            //
            // ⚠ Detach exactly those rows and nothing else. This was `ChangeTracker.Clear()`, which
            // empties the WHOLE request-scoped context — including whatever the caller had already
            // loaded before asking us to resolve a city. Every edit path has that shape ("load the
            // entity, resolve the picked city, then assign"), so the caller's later
            // SaveChangesAsync would find nothing tracked, write no row, and still report success:
            // silent data loss on a 200. Found while making an event's city editable (GH #136).
            //
            // Dependents first: detaching the city while its distance rows are still tracked severs
            // a required relationship, and EF refuses (the FKs aren't nullable).
            foreach (var distance in distances)
            {
                _db.Entry(distance).State = EntityState.Detached;
            }

            _db.Entry(city).State = EntityState.Detached;

            var winner = await _db.Cities.FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);
            if (winner is not null)
            {
                return winner;
            }

            throw;
        }
    }

    private async Task<List<CityPoint>> LoadOtherCityPointsAsync(CancellationToken ct) =>
        await _db.Cities
            .AsNoTracking()
            .Select(c => new CityPoint(c.Id, c.Latitude, c.Longitude))
            .ToListAsync(ct);

    /// <summary>Adds the distance rows for a new city and returns them, so a losing create race can
    /// detach exactly what it added rather than everything the context is tracking.</summary>
    private List<CityDistance> AddDistanceRows(City city, IReadOnlyList<CityPoint> others)
    {
        // Self-row: own-city entities rank nearest (distance 0). Required for the proximity join to
        // surface them (data-model.md).
        var rows = new List<CityDistance>(others.Count * 2 + 1)
        {
            new CityDistance { FromCityId = city.Id, ToCityId = city.Id, DistanceKm = 0 },
        };

        foreach (var other in others)
        {
            var km = HaversineKm(city.Latitude, city.Longitude, other.Latitude, other.Longitude);
            // Stored both ways so the proximity query is a single-sided join from any home city.
            rows.Add(new CityDistance { FromCityId = city.Id, ToCityId = other.Id, DistanceKm = km });
            rows.Add(new CityDistance { FromCityId = other.Id, ToCityId = city.Id, DistanceKm = km });
        }

        _db.CityDistances.AddRange(rows);
        return rows;
    }

    internal static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private readonly record struct CityPoint(Guid Id, double Latitude, double Longitude);
}
