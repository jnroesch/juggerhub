using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using JuggerHub.Common;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;

namespace JuggerHub.Services.Geocoding;

/// <summary>
/// Photon geocoder client (typed <see cref="HttpClient"/> over the Photon GeoJSON API) used in every
/// environment (feature 030). No API key — the geocoder is self-hosted. All resilience arrives from
/// the shared pipeline; this client carries none of its own (constitution Principle VII).
/// </summary>
/// <remarks>
/// The query text is user input, so it is never written to logs tied to a user (Principle I; the
/// resilience pipeline logs by integration name only). Only place-type results with a country and
/// coordinates become <see cref="GeocodedCity"/> — an unlocated hit can never become a stored City.
/// </remarks>
public sealed class PhotonGeocodingClient : IGeocodingClient
{
    // OSM place kinds we treat as selectable "cities". Photon tags these under osm_key = "place".
    private static readonly HashSet<string> CityPlaceValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "city", "town", "village", "municipality", "borough",
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly GeocodingOptions _options;
    private readonly ILogger<PhotonGeocodingClient> _logger;

    public PhotonGeocodingClient(
        HttpClient http, IOptions<GeocodingOptions> options, ILogger<PhotonGeocodingClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeocodedCity>> SearchAsync(
        string query, int limit, CancellationToken ct = default)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return [];
        }

        var cap = Math.Clamp(limit <= 0 ? _options.MaxResults : limit, 1, _options.MaxResults);
        // Photon's own limit is applied server-side; request a few extra so post-filtering to
        // city-type results still yields close to `cap` suggestions.
        var requested = Math.Min(cap * 3, 30);
        var url = $"api/?q={Uri.EscapeDataString(trimmed)}&limit={requested}"
            + $"&lang={Uri.EscapeDataString(_options.Language)}";

        var collection = await GetAsync(url, ct);
        return Project(collection, cap);
    }

    public async Task<GeocodedCity?> ResolveAsync(
        string externalId, string nameHint, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(nameHint))
        {
            return null;
        }

        // Re-query by the name hint and match on the authoritative provider id. The persisted city is
        // the geocoder's copy, never the caller's payload (Principle I). Widen the limit so the exact
        // id is present even for a common name.
        var results = await SearchAsync(nameHint, _options.MaxResults * 2, ct);
        return results.FirstOrDefault(c => string.Equals(c.ExternalId, externalId, StringComparison.Ordinal));
    }

    private async Task<PhotonCollection?> GetAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Status only — never the body or the query (Principle I). A non-success here is
                // treated as "unavailable" so callers degrade rather than surfacing provider detail.
                _logger.LogWarning(
                    "Geocoder returned status {Status}; treating as unavailable.", (int)response.StatusCode);
                throw new GeocodingUnavailableException("Geocoder returned an error status.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<PhotonCollection>(stream, JsonOptions, ct);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or BrokenCircuitException
            && !ct.IsCancellationRequested)
        {
            // Every attempt/retry/backoff the shared policy allows is spent by the time we reach here.
            // Surface a degradation signal; callers turn this into a retryable transient state.
            throw new GeocodingUnavailableException("Geocoder is unavailable.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Geocoder returned an unparseable response; treating as unavailable.");
            throw new GeocodingUnavailableException("Geocoder returned an unparseable response.", ex);
        }
    }

    private static IReadOnlyList<GeocodedCity> Project(PhotonCollection? collection, int cap)
    {
        if (collection?.Features is not { Count: > 0 } features)
        {
            return [];
        }

        var results = new List<GeocodedCity>(cap);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var feature in features)
        {
            var p = feature.Properties;
            var geometry = feature.Geometry;
            if (p is null || geometry?.Coordinates is not { Length: 2 } coords)
            {
                continue;
            }

            // Only real places (city/town/…) with a name and a country are usable.
            if (!CityPlaceValues.Contains(p.OsmValue ?? string.Empty)
                || string.IsNullOrWhiteSpace(p.Name)
                || string.IsNullOrWhiteSpace(p.Country)
                || p.OsmType is null
                || p.OsmId is null)
            {
                continue;
            }

            // Photon coordinates are [lon, lat].
            var lon = coords[0];
            var lat = coords[1];
            if (double.IsNaN(lat) || double.IsNaN(lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180)
            {
                continue;
            }

            var externalId = $"{p.OsmType}:{p.OsmId.Value.ToString(CultureInfo.InvariantCulture)}";
            if (!seen.Add(externalId))
            {
                continue;
            }

            results.Add(new GeocodedCity(
                ExternalId: externalId,
                Name: p.Name!,
                CountryName: p.Country!,
                CountryCode: string.IsNullOrWhiteSpace(p.CountryCode) ? null : p.CountryCode!.ToUpperInvariant(),
                Region: FirstNonEmpty(p.State, p.County),
                Latitude: lat,
                Longitude: lon));

            if (results.Count >= cap)
            {
                break;
            }
        }

        return results;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    // ---- Photon GeoJSON response shapes (only the fields we use) ----

    private sealed class PhotonCollection
    {
        [JsonPropertyName("features")]
        public List<PhotonFeature>? Features { get; set; }
    }

    private sealed class PhotonFeature
    {
        [JsonPropertyName("geometry")]
        public PhotonGeometry? Geometry { get; set; }

        [JsonPropertyName("properties")]
        public PhotonProperties? Properties { get; set; }
    }

    private sealed class PhotonGeometry
    {
        [JsonPropertyName("coordinates")]
        public double[]? Coordinates { get; set; }
    }

    private sealed class PhotonProperties
    {
        [JsonPropertyName("osm_id")]
        public long? OsmId { get; set; }

        [JsonPropertyName("osm_type")]
        public string? OsmType { get; set; }

        [JsonPropertyName("osm_value")]
        public string? OsmValue { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("countrycode")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("county")]
        public string? County { get; set; }
    }
}
