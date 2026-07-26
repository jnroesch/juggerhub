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
    // ExternalId, Name, cc, Country, Region, lat, lon
    private static readonly (string Id, string Name, string Cc, string Country, string Region, double Lat, double Lon)[] Fixture =
    [
        ("TEST:berlin", "Berlin", "DE", "Germany", "Berlin", 52.5200, 13.4050),
        ("TEST:köln", "Köln", "DE", "Germany", "North Rhine-Westphalia", 50.9384, 6.9600),
        ("TEST:hamburg", "Hamburg", "DE", "Germany", "Hamburg", 53.5511, 9.9937),
        ("TEST:münchen", "München", "DE", "Germany", "Bavaria", 48.1351, 11.5820),
        ("TEST:trier", "Trier", "DE", "Germany", "Rhineland-Palatinate", 49.7490, 6.6380),
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
                AlternateNames = string.Empty,
                CountryCode = c.Cc,
                CountryName = c.Country,
                Region = c.Region,
                Latitude = c.Lat,
                Longitude = c.Lon,
            });
        }

        await db.SaveChangesAsync();
    }
}
