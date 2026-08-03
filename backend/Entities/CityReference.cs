namespace JuggerHub.Entities;

/// <summary>
/// A row of the bundled GeoNames <c>cities500</c> reference dataset (feature 030, research R8) —
/// the search source behind the city picker. Seeded once per environment from a bundled snapshot;
/// never mutated at runtime. This is NOT the canonical selected-city: when a user picks one, its
/// data is copied into a <see cref="City"/> (with the distance-cache backfill). Keeping the ~225k
/// reference rows separate is what keeps <see cref="CityDistance"/> small.
/// <para>
/// The bundle deliberately excludes city districts (GeoNames feature code <c>PPLX</c>) so one city
/// is one option — searching "Hamburg" offers Hamburg, not Hamburg plus Hamburg-Nord,
/// Hamburg-Mitte and Hamburg-Altstadt. The filter lives in the regeneration script, not in the
/// query: there is no feature-code column here, and a district is simply never seeded.
/// </para>
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

    /// <summary>
    /// Inhabitant count from the GeoNames <c>cities500</c> dataset (feature 032). Drives relevance
    /// ranking of search options — more populous cities of a shared name lead. Non-negative;
    /// <c>0</c> means unknown/blank in the source and sorts last within a tier (<c>ORDER BY … DESC</c>).
    /// </summary>
    public int Population { get; set; }
}
