using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Entities;
using JuggerHub.Services.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// The signed-in user's notification preferences (feature 011). Every action is scoped to the
/// authenticated subject — the user is never a request parameter — so no request can read or change
/// another user's settings. The matrix GET applies opt-out defaults; the per-cell PUT auto-saves one
/// toggle. There is intentionally no endpoint for the always-on Security &amp; sign-in group.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notification-preferences")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class NotificationPreferencesController : ControllerBase
{
    private readonly INotificationPreferenceService _preferences;

    public NotificationPreferencesController(INotificationPreferenceService preferences) => _preferences = preferences;

    /// <summary>The caller's effective preference matrix.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationPreferenceMatrixDto>> Get(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _preferences.GetMatrixAsync(userId, ct));
    }

    /// <summary>Upsert one (category, channel) cell. Auto-save on toggle.</summary>
    [HttpPut("{category}/{channel}")]
    public async Task<IActionResult> Set(
        NotificationCategory category, NotificationChannel channel, [FromBody] SetPreferenceRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        // Route enum binding already rejects unknown names with 400; this guards defined-but-invalid values.
        if (!Enum.IsDefined(category) || !Enum.IsDefined(channel))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unknown preference",
                detail: "That notification category or channel isn't recognized.");
        }

        await _preferences.SetCellAsync(userId, category, channel, request.Enabled, ct);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
