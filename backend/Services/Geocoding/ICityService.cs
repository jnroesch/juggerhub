using JuggerHub.Dtos.Cities;
using JuggerHub.Entities;

namespace JuggerHub.Services.Geocoding;

/// <summary>
/// Backend surface for structured locations (feature 030): proxies city type-ahead search and
/// resolves a selected city into a persisted, de-duplicated <see cref="City"/> with its
/// city-to-city distance cache backfilled. The single place a City is ever created.
/// </summary>
public interface ICityService
{
    /// <summary>
    /// Type-ahead search for the picker. Never persists anything. When <paramref name="userId"/> is
    /// supplied and that user has a stored home city (feature 030), results are biased toward cities
    /// near it (feature 032); otherwise ranking falls back to population then name. The proximity
    /// origin is resolved server-side — the caller never supplies coordinates (Principle I).
    /// </summary>
    Task<IReadOnlyList<CityOptionDto>> SearchAsync(string query, int limit, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Every distinct country in the reference dataset, for the browse country filter's type-ahead,
    /// ordered by name. Deliberately not limited to countries that currently have records — filtering
    /// to an as-yet-empty country falls through to the results' empty state instead of the country
    /// being mysteriously missing from the picker.
    /// </summary>
    Task<IReadOnlyList<CountryDto>> ListCountriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Resolves a selected city to a persisted <see cref="City"/>, creating it (and its distance
    /// rows) on first use and reusing the cached row thereafter (FR-004, FR-022). The stored record
    /// comes from the geocoder, never from client-supplied fields (Principle I).
    /// </summary>
    /// <remarks>
    /// MUST be called before the caller tracks its own entity for the same save: on the rare
    /// create race it clears the change tracker to discard a losing insert.
    /// </remarks>
    /// <exception cref="CityNotResolvableException">The id is not present in the reference dataset.</exception>
    Task<City> ResolveAndUpsertAsync(string externalId, string? nameHint, CancellationToken ct = default);
}

/// <summary>A selected city id could not be resolved to a real, located city — mapped to 422.</summary>
public sealed class CityNotResolvableException : Exception
{
    public CityNotResolvableException(string externalId)
        : base($"City '{externalId}' could not be resolved.")
    {
    }
}
