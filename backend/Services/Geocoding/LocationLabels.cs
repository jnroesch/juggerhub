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
    /// Label for a search option. Normally <c>"City, Country"</c>; when <paramref name="disambiguate"/>
    /// is set (another result shares the same city name and country) and a region is known, the region
    /// is inserted — <c>"City, Region, Country"</c> — so same-named cities are distinguishable (FR-003).
    /// </summary>
    public static string Option(string name, string? region, string countryName, bool disambiguate) =>
        disambiguate && !string.IsNullOrWhiteSpace(region)
            ? $"{name}, {region}, {countryName}"
            : Display(name, countryName);

    /// <summary>Maps a persisted city to the read DTO; null in ⇒ null out (no location set).</summary>
    public static LocationDto? ToLocation(City? c) => c is null
        ? null
        : new LocationDto(c.ExternalId, c.Name, c.Region, c.CountryName, c.CountryCode, Display(c.Name, c.CountryName));
}
