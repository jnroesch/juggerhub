using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Auth;
using JuggerHub.Dtos.Profile;
using JuggerHub.Services.Auth;
using JuggerHub.Services.Profile;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Authentication endpoints (register / verify / login / logout / refresh /
/// forgot-password / reset-password / password-policy / me). Thin: validates input,
/// delegates to <see cref="IAuthService"/>, and shapes the HTTP response — including
/// setting/clearing the httpOnly auth cookies (the service never touches HttpContext).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    // Neutral message reused across enumeration-sensitive flows.
    private const string NeutralCheckEmail = "If that email can be registered, we've sent a message with next steps.";
    private const string NeutralResetSent = "If an account exists for that address, we've sent a password reset link.";

    private readonly IAuthService _auth;
    private readonly IProfileService _profiles;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService auth, IProfileService profiles, IWebHostEnvironment env)
    {
        _auth = auth;
        _profiles = profiles;
        _env = env;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ct);
        return result.Status switch
        {
            RegisterStatus.PasswordPolicyViolation => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Password does not meet the policy", detail: string.Join(" ", result.Errors)),
            RegisterStatus.HandleInvalid => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Handle is not valid", detail: string.Join(" ", result.Errors)),
            RegisterStatus.HandleTaken => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Handle unavailable", detail: string.Join(" ", result.Errors)),
            _ => Ok(new MessageResponse(NeutralCheckEmail)),
        };
    }

    /// <summary>
    /// Live handle availability/format check for the registration UI. Anonymous by
    /// design — handles are public identifiers, so this is not an account oracle.
    /// UX aid only; uniqueness is still enforced server-side at registration.
    /// </summary>
    [HttpGet("handle-available")]
    [AllowAnonymous]
    public async Task<ActionResult<HandleAvailabilityDto>> HandleAvailable([FromQuery] string handle, CancellationToken ct)
        => Ok(await _profiles.CheckHandleAsync(handle ?? string.Empty, ct));

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var ok = await _auth.VerifyEmailAsync(request, ct);
        return ok
            ? Ok(new MessageResponse("Your email address is verified. You can now sign in."))
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Verification failed",
                detail: "This verification link is invalid or has expired. Request a new one.");
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        await _auth.ResendVerificationAsync(request, ct);
        return Ok(new MessageResponse(NeutralCheckEmail));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ClientIp, ct);
        switch (result.Status)
        {
            case LoginStatus.Succeeded:
                SetAuthCookies(result.Tokens!.Value);
                return Ok(result.User);
            case LoginStatus.RequiresEmailVerification:
                return StatusCode(StatusCodes.Status403Forbidden, VerificationRequiredResponse.Default);
            case LoginStatus.Suspended:
                return StatusCode(StatusCodes.Status403Forbidden, AccountSuspendedResponse.Default);
            default:
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
                    detail: "Invalid email or password.");
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        Request.Cookies.TryGetValue(AuthCookieDefaults.RefreshTokenCookie, out var raw);
        var result = await _auth.RefreshAsync(raw, ClientIp, ct);
        if (result.Status != RefreshStatus.Succeeded)
        {
            ClearAuthCookies();
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
                detail: "Your session has expired. Please sign in again.");
        }

        SetAuthCookies(result.Tokens!.Value);
        return Ok(result.User);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        // Anonymous on purpose: an expired access token must not prevent sign-out.
        Request.Cookies.TryGetValue(AuthCookieDefaults.RefreshTokenCookie, out var raw);
        await _auth.LogoutAsync(raw, ct);
        ClearAuthCookies();
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);
        return Ok(new MessageResponse(NeutralResetSent));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _auth.ResetPasswordAsync(request, ClientIp, ct);
        return result.Status switch
        {
            ResetStatus.Success => Ok(new MessageResponse("Your password has been reset. You can now sign in.")),
            ResetStatus.PasswordPolicyViolation => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Password does not meet the policy", detail: string.Join(" ", result.Errors)),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Reset failed",
                detail: "This reset link is invalid or has expired. Request a new one."),
        };
    }

    [HttpGet("password-policy")]
    [AllowAnonymous]
    public ActionResult<PasswordPolicyDto> GetPasswordPolicy() => Ok(_auth.GetPasswordPolicy());

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken ct)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var user = await _auth.GetUserAsync(userId, ct);
        return user is null ? Unauthorized() : Ok(user);
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private void SetAuthCookies(IssuedTokens tokens)
    {
        var secure = !_env.IsDevelopment();
        Response.Cookies.Append(
            AuthCookieDefaults.AccessTokenCookie, tokens.AccessToken,
            AuthCookieDefaults.BuildAccessTokenCookieOptions(secure, tokens.AccessExpires, tokens.IsPersistent));
        Response.Cookies.Append(
            AuthCookieDefaults.RefreshTokenCookie, tokens.RefreshToken,
            AuthCookieDefaults.BuildRefreshTokenCookieOptions(secure, tokens.RefreshExpires, tokens.IsPersistent));
    }

    private void ClearAuthCookies()
    {
        var secure = !_env.IsDevelopment();
        Response.Cookies.Delete(
            AuthCookieDefaults.AccessTokenCookie,
            AuthCookieDefaults.BuildDeletionOptions(secure, "/"));
        Response.Cookies.Delete(
            AuthCookieDefaults.RefreshTokenCookie,
            AuthCookieDefaults.BuildDeletionOptions(secure, AuthCookieDefaults.RefreshTokenPath));
    }
}
