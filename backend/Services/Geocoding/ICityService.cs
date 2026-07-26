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
    /// <summary>Type-ahead search for the picker. Never persists anything.</summary>
    Task<IReadOnlyList<CityOptionDto>> SearchAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>
    /// The distinct countries that have at least one located team/event/player, for the browse
    /// country filter's type-ahead. Ordered by name. Derived from the canonical
    /// <see cref="City"/> table, so it only offers countries a filter can actually match.
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
