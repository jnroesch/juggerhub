using JuggerHub.Dtos.Cities;
using JuggerHub.Entities;

namespace JuggerHub.Services.Geocoding;

/// <summary>
/// Shared validation for a structured in-person address (feature 030, generalised in 042). One
/// implementation so events and trainings accept the same input and reject the same input — the
/// guarantee behind "a training and an event at the same address read identically" (042 SC-003).
/// </summary>
/// <remarks>
/// <para>
/// Pure functions in a static class rather than a DI-registered service: there is no state, no
/// lifetime and nothing to substitute. <see cref="ResolveCityAsync"/> takes
/// <see cref="ICityService"/> as a parameter for the same reason.
/// </para>
/// <para>
/// ⚠ Despite the namespace, nothing here makes a network call. <see cref="CityService"/> resolves
/// against the bundled, seeded <c>CityReference</c> table — a local SQL query, not an external
/// geocoder (030 research R8). Constitution Principle VII is therefore not engaged: no
/// <c>HttpClient</c>, timeout, retry or circuit breaker belongs on this path.
/// </para>
/// </remarks>
public static class StructuredAddress
{
    /// <summary>The validated address parts, or a user-facing <paramref name="Reason"/> to refuse.</summary>
    public readonly record struct AddressResult(
        string? VenueName, string? Street, string? PostalCode,
        string? VirtualLink, string? Reason);

    /// <summary>The resolved city (id + entity, so callers can set the FK nav), or a reason to refuse.</summary>
    public readonly record struct CityResult(Guid? CityId, City? City, string? Reason);

    /// <summary>
    /// Validates the address for an in-person subject, or the join link for a virtual one.
    /// A venue name stays optional; a street and postal code do not.
    /// </summary>
    /// <remarks>
    /// <c>subject</c> is what is being located — "event", "training" — so the refusal reads
    /// naturally. It is the only thing that differs between callers.
    /// </remarks>
    public static AddressResult Resolve(
        LocationKind kind, string? venueName, string? street, string? postalCode, string? virtualLink,
        string subject)
    {
        if (kind == LocationKind.InPerson)
        {
            var venue = Trimmed(venueName);
            var streetValue = Trimmed(street);
            var postalValue = Trimmed(postalCode);
            if (streetValue is null || postalValue is null)
            {
                return new AddressResult(null, null, null, null,
                    $"An in-person {subject} needs a street and postal code.");
            }

            return new AddressResult(venue, streetValue, postalValue, null, null);
        }

        var link = Trimmed(virtualLink);
        // Be lenient: accept links without an explicit scheme (e.g. "zoom.us/j/123") by
        // defaulting to https, and store the normalized absolute URL.
        if (link is not null && !link.Contains("://", StringComparison.Ordinal))
        {
            link = "https://" + link;
        }

        if (link is null || !Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            return new AddressResult(null, null, null, null,
                "Add a link like zoom.us/… or https://meet.… so people can join.");
        }

        return new AddressResult(null, null, null, link, null);
    }

    /// <summary>
    /// Resolves the picked city. In-person subjects need one; virtual subjects have none. The caller
    /// sends a <c>CityExternalId</c> and never a resolved id, name or coordinates — the server does
    /// the resolving (constitution Principle I).
    /// </summary>
    /// <remarks>
    /// Call this BEFORE the caller mutates its own entity for the same save: the first use of a city
    /// inserts it, and that <c>SaveChangesAsync</c> commits whatever else the context is holding —
    /// so an edit that assigns first and resolves second persists half of a change it may still go on
    /// to reject. (Resolution no longer detaches the caller's entities; a lost create race detaches
    /// only its own rows. It used to clear the whole change tracker, which silently voided the
    /// caller's update — see <see cref="CityService.ResolveAndUpsertAsync"/>.)
    /// </remarks>
    public static async Task<CityResult> ResolveCityAsync(
        ICityService cities, LocationKind kind, LocationSelectionDto? location, string subject,
        CancellationToken ct = default)
    {
        if (kind != LocationKind.InPerson)
        {
            return new CityResult(null, null, null);
        }

        if (string.IsNullOrWhiteSpace(location?.CityExternalId))
        {
            return new CityResult(null, null, $"An in-person {subject} needs a city.");
        }

        try
        {
            var city = await cities.ResolveAndUpsertAsync(location.CityExternalId!, location.Name, ct);
            return new CityResult(city.Id, city, null);
        }
        catch (CityNotResolvableException)
        {
            return new CityResult(null, null, "That city could not be found.");
        }
    }

    /// <summary>
    /// <c>"City, Country"</c> for a resolved city, or null when there is none. Callers compose their
    /// own legacy label from this: an event stores <c>"Online"</c> when virtual, a training stores
    /// null, so the composition itself is deliberately not shared.
    /// </summary>
    public static string? CityLabel(City? city) =>
        city is null ? null : LocationLabels.Display(city.Name, city.CountryName);

    private static string? Trimmed(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }
}
