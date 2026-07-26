using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Account;
using JuggerHub.Services.Account;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// The signed-in user's own account settings (feature 031). Every action is scoped to the
/// authenticated subject — the user is never a request parameter — so no request can change
/// another account. Thin: validates input and delegates to a service.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/account")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class AccountController : ControllerBase
{
    private readonly ILanguagePreferenceService _language;

    public AccountController(ILanguagePreferenceService language) => _language = language;

    /// <summary>
    /// Set the caller's preferred interface language. Validated against the supported allowlist
    /// (never trust the client). Does not touch the session — the user stays signed in (FR-015).
    /// </summary>
    [HttpPut("language")]
    public async Task<IActionResult> SetLanguage([FromBody] UpdateLanguageRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!SupportedLanguages.IsSupported(request.Language))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unsupported language",
                detail: "That language isn't one of the supported languages.");
        }

        var updated = await _language.SetAsync(userId, request.Language, ct);
        return updated ? NoContent() : Unauthorized();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
