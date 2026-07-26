namespace JuggerHub.Entities;

/// <summary>
/// A canonical, real-world city resolved from the self-hosted geocoder and persisted once for
/// reuse (feature 030). Referenced by <see cref="PlayerProfile"/>, <see cref="Team"/>, and
/// <see cref="Event"/> instead of the previous free-text location strings, so every location
/// carries a country and coordinates and can drive "near you" proximity.
/// </summary>
/// <remarks>
/// A City is created ONLY server-side by <c>CityService</c>, upserted on its <see cref="ExternalId"/>
/// (the geocoder's stable place id). Client-supplied name/coordinates are never trusted for storage
/// (constitution Principle I). Two same-named places in different regions/countries are distinct
/// rows. A City is created only when it has both a country and usable coordinates — an unlocated
/// result never becomes a City.
/// </remarks>
public sealed class City : BaseEntity
{
    /// <summary>
    /// Stable provider place identity (<c>"{osmType}:{osmId}"</c>, e.g. <c>"R:62578"</c>). The
    /// de-duplication key — unique index — and the handle used to re-resolve the city server-side.
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Canonical city name (e.g. "Köln"). Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Country display name (e.g. "Germany"). Required — a location with no country is not stored.</summary>
    public string CountryName { get; set; } = string.Empty;

    /// <summary>ISO-3166-1 alpha-2 code (e.g. "DE") when the provider supplies it; drives the country filter.</summary>
    public string? CountryCode { get; set; }

    /// <summary>State / county / admin area (e.g. "North Rhine-Westphalia"); disambiguates same-named cities.</summary>
    public string? Region { get; set; }

    /// <summary>Latitude in degrees, [-90, 90]. Required — anchors proximity distance.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude in degrees, [-180, 180]. Required.</summary>
    public double Longitude { get; set; }
}
