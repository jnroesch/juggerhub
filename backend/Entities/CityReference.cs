namespace JuggerHub.Entities;

/// <summary>
/// A row of the bundled GeoNames <c>cities500</c> reference dataset (feature 030, research R8) —
/// the search source behind the city picker. Seeded once per environment from a bundled snapshot;
/// never mutated at runtime. This is NOT the canonical selected-city: when a user picks one, its
/// data is copied into a <see cref="City"/> (with the distance-cache backfill). Keeping the ~235k
/// reference rows separate is what keeps <see cref="CityDistance"/> small.
/// </summary>
/// <remarks>
/// Not a <see cref="BaseEntity"/>: it carries the GeoNames id as its own key and has no audit
/// lifecycle. <see cref="ExternalId"/> (<c>"geonames:&lt;id&gt;"</c>) is the stable handle a picked
/// city references, matching <see cref="City.ExternalId"/>.
/// </remarks>
public sealed class CityReference
{
    /// <summary>Stable id, <c>"geonames:&lt;geonameId&gt;"</c>. Primary key.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>ASCII form (diacritic-free), used for accent-insensitive prefix search.</summary>
    public string AsciiName { get; set; } = string.Empty;

    /// <summary>Comma-separated Latin alternate/exonym names (e.g. "Munich" for München), for search.</summary>
    public string AlternateNames { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    /// <summary>Admin-1 region name (e.g. "North Rhine-Westphalia"); may be empty.</summary>
    public string Region { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
