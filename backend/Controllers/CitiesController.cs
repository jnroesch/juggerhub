using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Cities;
using JuggerHub.Services.Geocoding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JuggerHub.Controllers;

/// <summary>
/// City type-ahead search backing the picker (feature 030). Backend-proxied to the self-hosted
/// geocoder; the browser never calls the geocoder directly. Authentication required by default
/// (feature 026). Selecting a city is not done here — it happens on the owning profile/team/event
/// update, which re-resolves and persists the canonical city server-side.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cities")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class CitiesController : ControllerBase
{
    private readonly ICityService _cities;
    private readonly GeocodingOptions _options;

    public CitiesController(ICityService cities, IOptions<GeocodingOptions> options)
    {
        _cities = cities;
        _options = options.Value;
    }

    /// <summary>
    /// Type-ahead city search. Returns <c>[]</c> for a query shorter than the minimum (a normal
    /// "keep typing" state, not an error). Returns <c>503</c> when the geocoder is unavailable so
    /// the picker shows a retryable transient state rather than a broken control (FR-019).
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<CityOptionDto>>> Search(
        [FromQuery] string? q, CancellationToken ct)
    {
        var term = q?.Trim() ?? string.Empty;
        if (term.Length < _options.MinQueryLength)
        {
            return Ok(Array.Empty<CityOptionDto>());
        }

        try
        {
            var results = await _cities.SearchAsync(term, _options.MaxResults, ct);
            return Ok(results);
        }
        catch (GeocodingUnavailableException)
        {
            // Generic body only — no provider detail, no query echoed (Principle I).
            await ProblemResponse.WriteAsync(
                HttpContext,
                StatusCodes.Status503ServiceUnavailable,
                "https://tools.ietf.org/html/rfc7231#section-6.6.4",
                "City search unavailable",
                "City search is unavailable right now. Please try again in a moment.");
            return new EmptyResult();
        }
    }
}
