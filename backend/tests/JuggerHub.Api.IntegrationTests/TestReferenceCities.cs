using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests;

/// <summary>
/// Seeds a small <see cref="CityReference"/> fixture for integration tests (feature 030, R8),
/// standing in for the full bundled cities500 dataset (which the tests skip loading via
/// <c>Seeding:CityReferences=false</c>). Covers exactly the <c>TEST:*</c> ids the tests select by
/// name, with real-ish coordinates so proximity ordering is meaningful. Idempotent.
/// </summary>
public static class TestReferenceCities
{
    // ExternalId, Name, cc, Country, Region, lat, lon, population, alternateNames
    private static readonly (string Id, string Name, string Cc, string Country, string Region, double Lat, double Lon, int Population, string Alt)[] Fixture =
    [
        ("TEST:berlin", "Berlin", "DE", "Germany", "Berlin", 52.5200, 13.4050, 3_677_000, ""),
        ("TEST:köln", "Köln", "DE", "Germany", "North Rhine-Westphalia", 50.9384, 6.9600, 1_085_000, "Cologne"),
        ("TEST:hamburg", "Hamburg", "DE", "Germany", "Hamburg", 53.5511, 9.9937, 1_845_000, ""),
        ("TEST:münchen", "München", "DE", "Germany", "Bavaria", 48.1351, 11.5820, 1_488_000, "Munich"),
        ("TEST:trier", "Trier", "DE", "Germany", "Rhineland-Palatinate", 49.7490, 6.6380, 110_000, ""),
        // Two same-name, same-country cities: their labels fall back to including the region so they
        // stay distinguishable (feature 030, FR-003). Populations differ so feature-032 ranks the more
        // populous (Missouri) first — and their spread lets the proximity test flip that ordering.
        ("TEST:springfield-il", "Springfield", "US", "United States", "Illinois", 39.7817, -89.6501, 114_000, ""),
        ("TEST:springfield-mo", "Springfield", "US", "United States", "Missouri", 37.2090, -93.2923, 169_000, ""),
        // A small same-named city in a different country (feature 032): lets the populous-first test
        // assert the large German Berlin outranks a tiny US "Berlin".
        ("TEST:berlin-us", "Berlin", "US", "United States", "Connecticut", 41.6215, -72.7457, 20_000, ""),
        // Match-tier guard (feature 032, FR-002): "Zetaville" hits the term via its NAME (exact-prefix
        // tier) with a tiny population; "Megapolis" hits the same term only via an ALTERNATE name but is
        // huge. The name-tier hit must still rank first — population never crosses the tier boundary.
        ("TEST:zetaville", "Zetaville", "US", "United States", "A", 40.0000, -100.0000, 3_000, ""),
        ("TEST:megapolis", "Megapolis", "US", "United States", "B", 41.0000, -101.0000, 8_000_000, "Zetania"),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.CityReferences.AnyAsync())
        {
            return;
        }

        foreach (var c in Fixture)
        {
            db.CityReferences.Add(new CityReference
            {
                ExternalId = c.Id,
                Name = c.Name,
                AsciiName = new string(c.Name.Normalize(System.Text.NormalizationForm.FormD)
                    .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                        != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()),
                AlternateNames = c.Alt,
                CountryCode = c.Cc,
                CountryName = c.Country,
                Region = c.Region,
                Latitude = c.Lat,
                Longitude = c.Lon,
                Population = c.Population,
            });
        }

        await db.SaveChangesAsync();
    }
}
