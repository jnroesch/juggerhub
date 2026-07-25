namespace JuggerHub.Services.Geocoding;

/// <summary>
/// A city resolved from the geocoder — provider-neutral, transient (not persisted). Only results
/// carrying a country and usable coordinates are ever produced (feature 030).
/// </summary>
public sealed record GeocodedCity(
    string ExternalId,
    string Name,
    string CountryName,
    string? CountryCode,
    string? Region,
    double Latitude,
    double Longitude);

/// <summary>
/// Thin wrapper over the self-hosted geocoder (Photon). Backend-only; the browser never calls the
/// geocoder directly (feature 030, research R4). Resilience (timeouts, jittered retry of these
/// idempotent GETs, circuit breaker) is applied by the shared pipeline via
/// <c>AddJuggerHubResilience(config, "Geocoding")</c>, never here (constitution Principle VII).
/// </summary>
public interface IGeocodingClient
{
    /// <summary>
    /// Type-ahead search for cities matching <paramref name="query"/>. Returns up to
    /// <paramref name="limit"/> canonical city results, filtered to place-type results that have a
    /// country and coordinates. Throws <see cref="GeocodingUnavailableException"/> when the
    /// geocoder cannot be reached (after the resilience pipeline is exhausted).
    /// </summary>
    Task<IReadOnlyList<GeocodedCity>> SearchAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>
    /// Re-resolves a previously-offered city server-side, so the persisted record is authoritative
    /// rather than whatever a client posted (constitution Principle I). Photon has no lookup-by-id
    /// endpoint, so this re-queries by <paramref name="nameHint"/> and returns the result whose
    /// <see cref="GeocodedCity.ExternalId"/> equals <paramref name="externalId"/>, or <c>null</c> if
    /// none matches. Throws <see cref="GeocodingUnavailableException"/> when the geocoder is down.
    /// </summary>
    Task<GeocodedCity?> ResolveAsync(string externalId, string nameHint, CancellationToken ct = default);
}

/// <summary>
/// The geocoder could not be reached after the shared resilience pipeline was exhausted (timeout,
/// transient fault, or open circuit). Callers degrade gracefully — city search surfaces a retryable
/// transient error and proximity falls back to default ordering (feature 030, FR-018/FR-019).
/// </summary>
public sealed class GeocodingUnavailableException : Exception
{
    public GeocodingUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
