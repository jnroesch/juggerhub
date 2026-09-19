using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Services.Notifications.Push;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JuggerHub.Controllers;

/// <summary>
/// The browsers the signed-in member has enabled push notifications on (feature 055).
///
/// <para>
/// Every action is scoped to the authenticated subject — the owner is never a request parameter —
/// so one account can neither subscribe nor unsubscribe on another's behalf. There is deliberately
/// no endpoint listing a member's devices: the settings page speaks only about the browser it is
/// running in (spec FR-027).
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/push")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class PushSubscriptionsController : ControllerBase
{
    private readonly IPushSubscriptionService _subscriptions;
    private readonly IOptions<WebPushOptions> _options;

    public PushSubscriptionsController(
        IPushSubscriptionService subscriptions,
        IOptions<WebPushOptions> options)
    {
        _subscriptions = subscriptions;
        _options = options;
    }

    /// <summary>
    /// The VAPID public key this environment signs with. The browser needs it to subscribe, and it
    /// binds the resulting subscription to this environment — which is why a Dev key can never
    /// deliver to a Prod subscription.
    /// </summary>
    [HttpGet("public-key")]
    public ActionResult<PushPublicKeyDto> GetPublicKey()
        => Ok(new PushPublicKeyDto(_options.Value.PublicKey));

    /// <summary>Registers the browser the caller is using.</summary>
    [HttpPost("subscriptions")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPushSubscriptionRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!IsHttpsEndpoint(request.Endpoint))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid endpoint",
                detail: "A push endpoint must be an absolute https URL.");
        }

        await _subscriptions.RegisterAsync(
            userId, request.Endpoint, request.P256dh, request.Auth, request.DeviceLabel, ct);
        return NoContent();
    }

    /// <summary>
    /// Removes the caller's registration for one browser. Idempotent, and it never reports whether
    /// a row existed — that answer would be an oracle for which endpoints are registered.
    /// </summary>
    [HttpDelete("subscriptions")]
    public async Task<IActionResult> Remove(
        [FromBody] RemovePushSubscriptionRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _subscriptions.RemoveAsync(userId, request.Endpoint, ct);
        return NoContent();
    }

    private static bool IsHttpsEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
