using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Dtos.Parties;
using JuggerHub.Services.Parties;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Token-addressed party co-admin invitation flow (feature 016). The preview is anonymous (party /
/// team / event fields + inviter); accept/decline require authentication and team membership.
/// Accepting grants a party-admin role.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/party-invitations")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class PartyInvitationsController : ControllerBase
{
    private readonly IPartyInvitationService _invitations;

    public PartyInvitationsController(IPartyInvitationService invitations)
    {
        _invitations = invitations;
    }

    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<PartyInvitePreviewDto>> Preview(string token, CancellationToken ct)
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
            PartyOutcome.Ok => Ok(new { partyId = result.Value }),
            PartyOutcome.Forbidden => Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden", detail: result.Error ?? "Only a team member can co-run this party."),
            PartyOutcome.Full => Problem(statusCode: StatusCodes.Status409Conflict, title: "Party full", detail: result.Error),
            PartyOutcome.Closed => Problem(statusCode: StatusCodes.Status410Gone,
                title: "Invitation not usable", detail: result.Error ?? "This invite has expired or is no longer valid."),
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

        var result = await _invitations.DeclineAsync(token, userId, ct);
        return result.IsOk
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
