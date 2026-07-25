using JuggerHub.Dtos.Cities;
using JuggerHub.Entities;

namespace JuggerHub.Services.Geocoding;

/// <summary>
/// Builds the human-readable location strings used across the API (feature 030, FR-010). One place
/// so backend and the mirrored frontend helper stay in step.
/// </summary>
public static class LocationLabels
{
    /// <summary>Display label shown wherever a location appears: <c>"City, Country"</c>.</summary>
    public static string Display(string name, string countryName) => $"{name}, {countryName}";

    /// <summary>
    /// Disambiguation label for a search option: <c>"City, Region, Country"</c> (region omitted when
    /// absent) so same-named cities are distinguishable (FR-003).
    /// </summary>
    public static string Option(string name, string? region, string countryName) =>
        string.IsNullOrWhiteSpace(region)
            ? $"{name}, {countryName}"
            : $"{name}, {region}, {countryName}";

    public static CityOptionDto ToOption(GeocodedCity c) => new(
        c.ExternalId, c.Name, c.Region, c.CountryName, c.CountryCode,
        Option(c.Name, c.Region, c.CountryName), c.Latitude, c.Longitude);

    /// <summary>Maps a persisted city to the read DTO; null in ⇒ null out (no location set).</summary>
    public static LocationDto? ToLocation(City? c) => c is null
        ? null
        : new LocationDto(c.ExternalId, c.Name, c.Region, c.CountryName, c.CountryCode, Display(c.Name, c.CountryName));
}
