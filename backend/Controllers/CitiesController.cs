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
    /// Type-ahead city search over the bundled cities500 reference table (feature 030, R8). Returns
    /// <c>[]</c> for a query shorter than the minimum (a normal "keep typing" state, not an error).
    /// It's a local query, so there is no external-service failure mode to degrade for.
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

        return Ok(await _cities.SearchAsync(term, _options.MaxResults, ct));
    }

    /// <summary>
    /// Every distinct country in the reference dataset (feature 030), backing the browse country
    /// filter's type-ahead. Not limited to countries with existing records — an empty country just
    /// yields the results' empty state.
    /// </summary>
    [HttpGet("countries")]
    public async Task<ActionResult<IReadOnlyList<CountryDto>>> Countries(CancellationToken ct)
        => Ok(await _cities.ListCountriesAsync(ct));
}
