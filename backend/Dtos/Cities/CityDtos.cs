namespace JuggerHub.Dtos.Cities;

/// <summary>
/// A transient city search result offered to the picker (feature 030). Not persisted — selecting it
/// sends <see cref="ExternalId"/> back, and the server re-resolves the canonical city itself. The
/// latitude/longitude here are display hints only and are never trusted for storage (Principle I).
/// </summary>
public sealed record CityOptionDto(
    string ExternalId,
    string Name,
    string? Region,
    string CountryName,
    string? CountryCode,
    string Label,
    double Latitude,
    double Longitude);

/// <summary>
/// The shared, read-only location shape shown everywhere a profile/team/event location appears
/// (feature 030). Null wherever no city is set. <see cref="Label"/> is the "City, Country" string
/// (FR-010).
/// </summary>
public sealed record LocationDto(
    // The provider place id, echoed so an edit form can resend the current city without re-picking.
    string ExternalId,
    string Name,
    string? Region,
    string CountryName,
    string? CountryCode,
    string Label);

/// <summary>
/// The write fragment embedded in profile/team/event update DTOs to set or clear a location.
/// <see cref="CityExternalId"/> null clears the location; a value selects that city. <see cref="Name"/>
/// is only a re-resolution hint for the geocoder — never persisted verbatim.
/// </summary>
public sealed record LocationSelectionDto(
    string? CityExternalId,
    string? Name);
