using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Home;
using JuggerHub.Services.Home;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// The signed-in player's Home dashboard (feature 008) — a cross-resource read *view*, not a
/// resource. All actions require the JWT-in-cookie scheme and act only on the authenticated
/// subject; every module is entitlement-scoped server-side in <see cref="IHomeService"/>.
/// RSVP is not here — it reuses the existing event sign-up endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/home")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class HomeController : ControllerBase
{
    private readonly IHomeService _home;

    public HomeController(IHomeService home)
    {
        _home = home;
    }

    /// <summary>Composite dashboard: viewer summary + capped top-N per module. First-paint path.</summary>
    [HttpGet]
    public async Task<ActionResult<HomeDto>> Get(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _home.GetHomeAsync(userId, ct));
    }

    /// <summary>The caller's full upcoming-events list ("see all"), paginated.</summary>
    [HttpGet("up-next")]
    public async Task<ActionResult<PagedResult<UpNextItemDto>>> UpNext(
        [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _home.ListUpNextAsync(userId, pagination, ct));
    }

    /// <summary>The caller's aggregated news feed ("see all"), paginated.</summary>
    [HttpGet("news")]
    public async Task<ActionResult<PagedResult<HomeNewsDto>>> News(
        [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _home.ListNewsAsync(userId, pagination, ct));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
