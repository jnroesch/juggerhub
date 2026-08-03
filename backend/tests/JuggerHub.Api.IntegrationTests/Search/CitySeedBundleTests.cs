using System.IO.Compression;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// Guards the CONTENT of the bundled cities500 seed rather than any runtime behaviour, so it needs
/// neither the API nor a database (no collection fixture, no container).
/// <para>
/// The picker must offer one option per city: searching "Hamburg" gives Hamburg, not Hamburg plus
/// its boroughs. That is enforced only by the feature-code filter in
/// <c>Data/Seed/regenerate-cities500.mjs</c>, which lives outside the C# build and is exercised by
/// hand every few months — so a regeneration that loses the filter would otherwise ship silently and
/// only surface as "why are there four Hamburgs again?". The counter-assertions catch the opposite
/// mistake, an over-broad filter that quietly guts the dataset.
/// </para>
/// </summary>
public sealed class CitySeedBundleTests
{
    private const string SeedPath = "Data/Seed/cities500.seed.tsv.gz";

    /// <summary>Districts (GeoNames <c>PPLX</c>) that were in the bundle before the filter existed.</summary>
    private static readonly string[] KnownDistricts =
        ["Hamburg-Nord", "Hamburg-Mitte", "Hamburg-Altstadt", "Kowloon", "Setagaya", "Hong Kong Island"];

    /// <summary>Real cities that must survive it — including each district's parent.</summary>
    private static readonly string[] MustSurvive = ["Hamburg", "Berlin", "Köln", "Hong Kong", "Tokyo"];

    [Fact]
    public void Bundle_offers_one_option_per_city_and_keeps_the_real_ones()
    {
        var names = ReadNames();

        Assert.All(KnownDistricts, district =>
            Assert.DoesNotContain(district, names.Keys));

        Assert.All(MustSurvive, city =>
            Assert.True(names.ContainsKey(city), $"'{city}' is missing — the seed filter is too broad."));

        // The complaint that started this: one Hamburg in Germany, not four.
        Assert.Single(names["Hamburg"], cc => cc == "DE");

        // A filter that removed most of the dataset would still satisfy the assertions above.
        Assert.True(names.Count > 100_000, $"Only {names.Count} distinct city names in the bundle.");
    }

    /// <summary>City name → the country codes it appears under.</summary>
    private static Dictionary<string, List<string>> ReadNames()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SeedPath);
        Assert.True(File.Exists(path), $"Seed bundle not found at {path}.");

        var names = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        using var file = File.OpenRead(path);
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);

        while (reader.ReadLine() is string line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            // Columns mirror the COPY in CityReferenceSeeder: 1 = Name, 4 = CountryCode.
            var c = line.Split('\t');
            if (c.Length < 10)
            {
                continue;
            }

            if (!names.TryGetValue(c[1], out var countries))
            {
                names[c[1]] = countries = [];
            }

            countries.Add(c[4]);
        }

        return names;
    }
}
