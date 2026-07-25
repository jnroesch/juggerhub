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
        if (city is null)
        {
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
        }

        return city;
    }
}
