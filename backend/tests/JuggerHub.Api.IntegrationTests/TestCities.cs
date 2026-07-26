using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests;

/// <summary>
/// Test helper for structured locations (feature 030). Creates/reuses a canonical <see cref="City"/>
/// by name so builders can link the FK instead of the removed free-text city/hometown strings. Uses
/// a deterministic <c>TEST:{name}</c> external id and plausible coordinates.
/// </summary>
public static class TestCities
{
    private static readonly Dictionary<string, (double Lat, double Lon)> KnownCoords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Berlin"] = (52.5200, 13.4050),
        ["Köln"] = (50.9384, 6.9600),
        ["Cologne"] = (50.9384, 6.9600),
        ["Hamburg"] = (53.5511, 9.9937),
        ["München"] = (48.1351, 11.5820),
        ["Munich"] = (48.1351, 11.5820),
    };

    public static async Task<City> GetOrCreateAsync(AppDbContext db, string name)
    {
        var externalId = "TEST:" + name.ToLowerInvariant();
        var city = await db.Cities.FirstOrDefaultAsync(c => c.ExternalId == externalId);
        if (city is not null)
        {
            return city;
        }

        var (lat, lon) = KnownCoords.TryGetValue(name, out var c) ? c : (52.52, 13.405);
        city = new City
        {
            ExternalId = externalId,
            Name = name,
            CountryName = "Germany",
            CountryCode = "DE",
            Region = null,
            Latitude = lat,
            Longitude = lon,
        };
        db.Cities.Add(city);

        // Backfill city-to-city distances (self-row + both directions) so proximity ordering works
        // over test-seeded cities, mirroring CityService. Each city is created once (early return
        // above), so pair rows are never inserted twice — no unique-index conflict.
        db.CityDistances.Add(new CityDistance { FromCityId = city.Id, ToCityId = city.Id, DistanceKm = 0 });
        var others = await db.Cities.AsNoTracking()
            .Where(o => o.Id != city.Id)
            .Select(o => new { o.Id, o.Latitude, o.Longitude })
            .ToListAsync();
        foreach (var o in others)
        {
            var km = HaversineKm(lat, lon, o.Latitude, o.Longitude);
            db.CityDistances.Add(new CityDistance { FromCityId = city.Id, ToCityId = o.Id, DistanceKm = km });
            db.CityDistances.Add(new CityDistance { FromCityId = o.Id, ToCityId = city.Id, DistanceKm = km });
        }

        return city;
    }

    private const double EarthRadiusKm = 6371.0088;

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
