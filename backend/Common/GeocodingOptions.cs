namespace JuggerHub.Common;

/// <summary>
/// Configuration for the self-hosted geocoder integration (feature 030). Bound from the
/// <c>Geocoding</c> section. Holds only the provider's in-network address and query shaping —
/// no secrets (the geocoder is self-hosted and unauthenticated), so nothing here is sensitive.
/// </summary>
/// <remarks>
/// The <em>resilience</em> limits for outbound calls live separately in
/// <see cref="ResilienceOptions"/> (section <c>Resilience:Outbound:Geocoding</c>), inherited via
/// <c>AddJuggerHubResilience(config, "Geocoding")</c> — this type never re-implements timeouts or
/// retry (constitution Principle VII).
/// </remarks>
public sealed class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    /// <summary>Base URL of the Photon geocoder (e.g. <c>http://photon:2322</c>).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Max suggestions returned by a city search. Server-capped so a client cannot widen it.</summary>
    public int MaxResults { get; set; } = 8;

    /// <summary>Minimum query length before the geocoder is queried; below it, an empty list is returned.</summary>
    public int MinQueryLength { get; set; } = 2;

    /// <summary>Language hint sent to the geocoder for localized place names.</summary>
    public string Language { get; set; } = "en";
}
