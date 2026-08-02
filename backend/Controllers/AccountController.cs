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
    private readonly IAccountDeletionService _deletion;

    public AccountController(ILanguagePreferenceService language, IAccountDeletionService deletion)
    {
        _language = language;
        _deletion = deletion;
    }

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

    /// <summary>
    /// What deleting your account would do, and whether you may (feature 037 US2). Mutates nothing;
    /// everything it reports is re-checked at confirmation, so a blocker acquired in between is
    /// caught there rather than half-applied.
    /// </summary>
    [HttpGet("deletion-preview")]
    public async Task<ActionResult<AccountDeletionPreviewDto>> DeletionPreview(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var preview = await _deletion.PreviewAsync(userId, ct);

        // Null means suspended, banned, or already erased. One generic refusal for all three — which
        // it is, is not the caller's business (Principle I).
        return preview is null
            ? Problem(statusCode: StatusCodes.Status403Forbidden, title: "Not available",
                detail: "Account deletion isn't available for this account.")
            : Ok(preview);
    }

    /// <summary>
    /// Delete your own account (feature 037). Immediate, irreversible, and all-or-nothing.
    /// </summary>
    /// <remarks>
    /// <b>There is no account identifier anywhere in this shape.</b> The subject is always the
    /// authenticated caller, so no request exists in which one member could target another (FR-002).
    /// On success the auth cookies are cleared and nothing is returned — there is no longer an
    /// account to describe.
    /// </remarks>
    [HttpPost("deletion")]
    public async Task<IActionResult> Delete([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _deletion.DeleteAsync(userId, request.Password, request.Confirmation, ct);

        switch (result.Outcome)
        {
            case AccountDeletionOutcome.Done:
                // The session must not outlive the account it belonged to.
                Response.Cookies.Delete(AuthCookieDefaults.AccessTokenCookie);
                Response.Cookies.Delete(AuthCookieDefaults.RefreshTokenCookie);
                return NoContent();

            case AccountDeletionOutcome.ConfirmationMismatch:
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Confirmation didn't match",
                    detail: "Type the confirmation word exactly as shown.");

            case AccountDeletionOutcome.PasswordRejected:
                // Generic, and identical to any other failed credential check — a wrong password and a
                // locked-out account must not be tellable apart.
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Couldn't verify you",
                    detail: "That password didn't match.");

            case AccountDeletionOutcome.NotEligible:
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Not available",
                    detail: "Account deletion isn't available for this account.");

            case AccountDeletionOutcome.Blocked:
                // 409 with the COMPLETE blocker list, so the member can clear them in one pass
                // (FR-011). Nothing was changed.
                return Conflict(new
                {
                    title = "Account deletion blocked",
                    status = StatusCodes.Status409Conflict,
                    blockers = result.Blockers,
                });

            default:
                return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Deletion failed",
                    detail: "We couldn't delete your account just now. Nothing was changed — please try again.");
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
