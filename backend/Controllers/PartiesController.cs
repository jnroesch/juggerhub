using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Parties;
using JuggerHub.Services.Parties;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Event parties (feature 016). Forming a party requires team-admin; every roster/manage action is
/// gated on party-admin or team-membership server-side (constitution Principle I). Member-gated
/// reads return 404 to non-members so a party's existence never leaks.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/parties")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class PartiesController : ControllerBase
{
    private readonly IPartyService _parties;
    private readonly IPartyRosterService _roster;
    private readonly IPartyNewsService _news;
    private readonly IPartyInvitationService _invitations;

    public PartiesController(
        IPartyService parties, IPartyRosterService roster, IPartyNewsService news, IPartyInvitationService invitations)
    {
        _parties = parties;
        _roster = roster;
        _news = news;
        _invitations = invitations;
    }

    // --- Lifecycle ------------------------------------------------------------

    [HttpPost]
    public async Task<ActionResult<PartyDto>> Form([FromBody] FormPartyRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _parties.FormAsync(request.EventId, request.TeamId, request.Message, userId, ct);
        return result.IsOk
            ? Created($"/api/v1/parties/{result.Value!.Id}", result.Value)
            : Fail(result.Outcome, result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PartyDto>> Get(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var dto = await _parties.GetDetailAsync(id, userId, ct);
        return dto is null ? PartyNotFound() : Ok(dto);
    }

    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<PartyDto>> Apply(Guid id, CancellationToken ct) =>
        await MutateAsync(userId => _parties.ApplyAsync(id, userId, ct));

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<PartyDto>> Withdraw(Guid id, CancellationToken ct) =>
        await MutateAsync(userId => _parties.WithdrawAsync(id, userId, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Disband(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _parties.DisbandAsync(id, userId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    // --- Roster ---------------------------------------------------------------

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<PagedResult<PartyMemberDto>>> Members(
        Guid id, [FromQuery] string group, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!TryParseGroup(group, out var parsed))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid group",
                detail: "group must be one of: in, declined, noResponse.");
        }

        var page = await _roster.ListGroupAsync(id, parsed, userId, pagination, ct);
        return page is null ? PartyNotFound() : Ok(page);
    }

    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<PartyMemberDto>> Join(Guid id, CancellationToken ct) =>
        await MutateMemberAsync(userId => _roster.JoinAsync(id, userId, ct));

    [HttpPost("{id:guid}/decline")]
    public async Task<ActionResult<PartyMemberDto>> Decline(Guid id, CancellationToken ct) =>
        await MutateMemberAsync(userId => _roster.DeclineAsync(id, userId, ct));

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _roster.LeaveAsync(id, userId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    [HttpDelete("{id:guid}/members/{targetUserId:guid}")]
    public async Task<IActionResult> Remove(Guid id, Guid targetUserId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _roster.RemoveAsync(id, targetUserId, userId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    [HttpPost("{id:guid}/members/{targetUserId:guid}/nudge")]
    public async Task<IActionResult> Nudge(Guid id, Guid targetUserId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _roster.NudgeAsync(id, targetUserId, userId, ct);
        return result.IsOk ? Accepted() : Fail(result.Outcome, result.Error);
    }

    // --- News -----------------------------------------------------------------

    [HttpGet("{id:guid}/news")]
    public async Task<ActionResult<PagedResult<PartyNewsDto>>> News(
        Guid id, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var page = await _news.ListAsync(id, userId, pagination, ct);
        return page is null ? PartyNotFound() : Ok(page);
    }

    [HttpPost("{id:guid}/news")]
    public async Task<ActionResult<PartyNewsDto>> PostNews(Guid id, [FromBody] CreatePartyNewsRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _news.CreateAsync(id, request.Body, userId, ct);
        return result.IsOk ? Created($"/api/v1/parties/{id}/news", result.Value) : Fail(result.Outcome, result.Error);
    }

    // --- Co-admin invitations -------------------------------------------------

    [HttpGet("{id:guid}/invitations/link")]
    public async Task<ActionResult<PartyInviteLinkDto>> GetInviteLink(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.GetActiveLinkAsync(id, userId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPost("{id:guid}/invitations/link")]
    public async Task<ActionResult<PartyInviteLinkDto>> RotateInviteLink(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.CreateOrRotateLinkAsync(id, userId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpGet("{id:guid}/invitations")]
    public async Task<ActionResult<PagedResult<PartyInvitationDto>>> GetInvitations(
        Guid id, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.ListPendingAsync(id, userId, pagination, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<PartyInvitationDto>> CreateInvitation(
        Guid id, [FromBody] CreatePartyInviteRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.CreateTargetedAsync(id, userId, request.UserId, ct);
        return result.IsOk ? Created($"/api/v1/parties/{id}/invitations", result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid id, Guid invitationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.RevokeAsync(id, userId, invitationId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    [HttpGet("{id:guid}/invitations/member-search")]
    public async Task<ActionResult<PagedResult<PartyInvitableUserDto>>> SearchMembers(
        Guid id, [FromQuery] string query, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.SearchMembersAsync(id, userId, query, pagination, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<ActionResult<PartyDto>> MutateAsync(Func<Guid, Task<PartyResult<PartyDto>>> op)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await op(userId);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    private async Task<ActionResult<PartyMemberDto>> MutateMemberAsync(Func<Guid, Task<PartyResult<PartyMemberDto>>> op)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await op(userId);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    private ObjectResult Fail(PartyOutcome outcome, string? detail) => outcome switch
    {
        PartyOutcome.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: detail ?? "No such party."),
        PartyOutcome.Forbidden => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail ?? "Not allowed."),
        PartyOutcome.NotTeamAdmin => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail ?? "Only a team admin can do this."),
        PartyOutcome.Invalid => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: detail),
        PartyOutcome.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail),
        PartyOutcome.Full => Problem(statusCode: StatusCodes.Status409Conflict, title: "Party full", detail: detail),
        PartyOutcome.Closed => Problem(statusCode: StatusCodes.Status409Conflict, title: "Closed", detail: detail),
        _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Request failed", detail: detail),
    };

    private ObjectResult PartyNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Party not found", detail: "No party matches that address.");

    private static bool TryParseGroup(string? group, out PartyRosterGroup parsed)
    {
        switch ((group ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "in": parsed = PartyRosterGroup.In; return true;
            case "declined": parsed = PartyRosterGroup.Declined; return true;
            case "noresponse": parsed = PartyRosterGroup.NoResponse; return true;
            default: parsed = default; return false;
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
