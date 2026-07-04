using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Dtos.Events;
using JuggerHub.Services.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Token-addressed co-admin invitation flow. The preview is anonymous (public event fields +
/// inviter); accept/decline require authentication. Accepting grants an event-admin role.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/event-invitations")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class EventInvitationsController : ControllerBase
{
    private readonly IEventInvitationService _invitations;

    public EventInvitationsController(IEventInvitationService invitations)
    {
        _invitations = invitations;
    }

    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventInvitePreviewDto>> Preview(string token, CancellationToken ct)
    {
        var dto = await _invitations.GetPreviewAsync(token, ct);
        return dto is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Invitation not found", detail: "This invite link is not valid.")
            : Ok(dto);
    }

    [HttpPost("{token}/accept")]
    public async Task<IActionResult> Accept(string token, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.AcceptAsync(token, userId, ct);
        return result.Outcome switch
        {
            AcceptOutcome.Granted => Ok(new { eventId = result.EventId }),
            AcceptOutcome.AlreadyAdmin => Ok(new { eventId = result.EventId }),
            AcceptOutcome.NotUsable => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Invitation not usable", detail: "This invite has expired or is no longer valid."),
            _ => Problem(statusCode: StatusCodes.Status404NotFound, title: "Invitation not found", detail: "This invite link is not valid."),
        };
    }

    [HttpPost("{token}/decline")]
    public async Task<IActionResult> Decline(string token, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var outcome = await _invitations.DeclineAsync(token, userId, ct);
        return outcome == DeclineOutcome.Declined
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Invitation not found", detail: "This invite link is not valid.");
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
