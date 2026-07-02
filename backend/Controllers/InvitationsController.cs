using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Dtos.Teams;
using JuggerHub.Services.Teams;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// The invitee's token flow. The preview is anonymous and carries only public team info +
/// the inviter (no roster/news). Accept/decline require authentication and act on the
/// authenticated subject; an unauthenticated visitor is challenged (401) so the SPA can send
/// them to sign in/register and return to the same invite.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/invitations")]
public sealed class InvitationsController : ControllerBase
{
    private readonly ITeamInvitationService _invitations;

    public InvitationsController(ITeamInvitationService invitations) => _invitations = invitations;

    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<InvitePreviewDto>> GetPreview(string token, CancellationToken ct)
    {
        var preview = await _invitations.GetPreviewAsync(token, ct);
        return preview is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Invite not found",
                detail: "This invite link isn't valid.")
            : Ok(preview);
    }

    [HttpPost("{token}/accept")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AcceptInviteResultDto>> Accept(string token, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.AcceptAsync(token, userId, ct);
        return result.Status switch
        {
            AcceptStatus.Joined or AcceptStatus.AlreadyMember => Ok(new AcceptInviteResultDto(result.TeamSlug!)),
            AcceptStatus.NotUsable => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Invite unavailable", detail: "This invite has expired or is no longer valid."),
            _ => Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Invite not found", detail: "This invite link isn't valid."),
        };
    }

    [HttpPost("{token}/decline")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Decline(string token, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _invitations.DeclineAsync(token, userId, ct);
        return status == DeclineStatus.Declined
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Invite not found",
                detail: "This invite link isn't valid.");
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
