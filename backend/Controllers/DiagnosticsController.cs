using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Dtos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Protected sample endpoint that proves the auth/authorization pipeline is
/// enforced server-side (US2). No auth UI/endpoint issues the cookie yet, so in
/// the walking skeleton this returns 401 in normal use.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
// Bind explicitly to JwtBearer so an absent/invalid cookie yields 401 (not an
// Identity cookie redirect), regardless of any other registered scheme.
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class DiagnosticsController : ControllerBase
{
    /// <summary>
    /// GET /api/v1/diagnostics/whoami — echoes the authenticated principal's id.
    /// Requires a valid JWT carried in the httpOnly auth cookie.
    /// </summary>
    [HttpGet("whoami")]
    public ActionResult<WhoAmIDto> WhoAmI()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(subject, out var userId)
            ? Ok(new WhoAmIDto(userId, true))
            : Ok(new WhoAmIDto(Guid.Empty, true));
    }
}
