namespace JuggerHub.Common;

/// <summary>
/// Configuration for city search (feature 030, research R8). Search runs against the bundled,
/// seeded <c>CityReference</c> table — there is no external service, so this holds only query
/// shaping. Bound from the <c>Geocoding</c> section. No secrets.
/// </summary>
public sealed class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    /// <summary>Max suggestions returned by a city search. Server-capped so a client cannot widen it.</summary>
    public int MaxResults { get; set; } = 8;

    /// <summary>Minimum query length before searching; below it, an empty list is returned.</summary>
    public int MinQueryLength { get; set; } = 2;
}
