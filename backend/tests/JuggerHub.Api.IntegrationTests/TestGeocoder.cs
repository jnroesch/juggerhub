using JuggerHub.Services.Geocoding;

namespace JuggerHub.Api.IntegrationTests;

/// <summary>
/// In-memory <see cref="IGeocodingClient"/> for integration tests (feature 030) — no live Photon
/// needed. Resolves a small set of known cities by name/id (deterministic <c>TEST:{name}</c> ids
/// matching <see cref="TestCities"/>) and supports prefix search over them. Toggle
/// <see cref="Unavailable"/> to exercise the graceful-degradation paths (FR-018/FR-019).
/// </summary>
public sealed class TestGeocoder : IGeocodingClient
{
    private static readonly GeocodedCity[] Known =
    [
        new("TEST:berlin", "Berlin", "Deutschland", "DE", "Berlin", 52.5200, 13.4050),
        new("TEST:köln", "Köln", "Deutschland", "DE", "Nordrhein-Westfalen", 50.9384, 6.9600),
        new("TEST:cologne", "Cologne", "Deutschland", "DE", "Nordrhein-Westfalen", 50.9384, 6.9600),
        new("TEST:hamburg", "Hamburg", "Deutschland", "DE", "Hamburg", 53.5511, 9.9937),
        new("TEST:münchen", "München", "Deutschland", "DE", "Bayern", 48.1351, 11.5820),
        new("TEST:munich", "Munich", "Deutschland", "DE", "Bayern", 48.1351, 11.5820),
    ];

    /// <summary>When true, every call throws <see cref="GeocodingUnavailableException"/>.</summary>
    public bool Unavailable { get; set; }

    public Task<IReadOnlyList<GeocodedCity>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        if (Unavailable)
        {
            throw new GeocodingUnavailableException("Test geocoder is unavailable.");
        }

        IReadOnlyList<GeocodedCity> matches = Known
            .Where(c => c.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
        return Task.FromResult(matches);
    }

    public Task<GeocodedCity?> ResolveAsync(string externalId, string nameHint, CancellationToken ct = default)
    {
        if (Unavailable)
        {
            throw new GeocodingUnavailableException("Test geocoder is unavailable.");
        }

        var known = Known.FirstOrDefault(c => c.ExternalId == externalId);
        if (known is not null)
        {
            return Task.FromResult<GeocodedCity?>(known);
        }

        // Synthesize any other `TEST:{name}` id so tests can create teams/events in arbitrary cities
        // (Trier, Ulm, "Evt1234", …) through the real API without pre-registering each one here. The
        // name comes from the id suffix (matching TestCities' `TEST:{name.ToLower()}` convention);
        // coordinates are deterministic per name so distance is stable.
        if (externalId.StartsWith("TEST:", StringComparison.Ordinal))
        {
            var slug = externalId["TEST:".Length..];
            var name = string.IsNullOrEmpty(nameHint)
                ? (slug.Length == 0 ? slug : char.ToUpperInvariant(slug[0]) + slug[1..])
                : nameHint;
            var hash = Math.Abs(slug.GetHashCode());
            var lat = 47.0 + (hash % 700) / 100.0;   // ~47–54°N (central Europe band)
            var lon = 6.0 + (hash / 700 % 900) / 100.0; // ~6–15°E
            return Task.FromResult<GeocodedCity?>(
                new GeocodedCity(externalId, name, "Deutschland", "DE", null, lat, lon));
        }

        return Task.FromResult<GeocodedCity?>(null);
    }
}
