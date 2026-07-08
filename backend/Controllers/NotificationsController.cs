using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Services.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// The signed-in user's in-app notifications (feature 010). Every action is scoped to the
/// authenticated subject — the recipient is never a request parameter — so no request can read or
/// mutate another user's notifications. Real-time delivery rides the SignalR hub; this REST surface
/// backs initial load, pagination, unread count, and mark-read, and keeps the system fully correct
/// when the hub is unavailable.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    /// <summary>The caller's notifications, newest-first, paginated.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List(
        [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _notifications.ListAsync(userId, pagination, ct));
    }

    /// <summary>The caller's current unread count (the bell badge).</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(new UnreadCountDto(await _notifications.CountUnreadAsync(userId, ct)));
    }

    /// <summary>Mark one notification read. Idempotent; 404 when it isn't the caller's.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var ok = await _notifications.MarkReadAsync(userId, id, ct);
        return ok
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Notification not found",
                detail: "This notification doesn't exist.");
    }

    /// <summary>Mark all the caller's unread notifications read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _notifications.MarkAllReadAsync(userId, ct);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
