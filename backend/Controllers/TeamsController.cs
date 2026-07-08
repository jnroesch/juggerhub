using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Search;
using JuggerHub.Dtos.Teams;
using JuggerHub.Services.Search;
using JuggerHub.Services.Teams;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Team space + member handling. Internal reads (detail/roster/activity/news/invitations)
/// require an authenticated MEMBER; a non-member and an unknown team both yield 404 (no
/// membership oracle). Mutations require the ADMIN role, enforced server-side in the service
/// layer. Only <c>{slug}/public</c> is anonymous (constitution Principle I).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teams")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class TeamsController : ControllerBase
{
    private readonly ITeamService _teams;
    private readonly ITeamActivityService _activity;
    private readonly ITeamNewsService _news;
    private readonly ITeamInvitationService _invitations;
    private readonly ITeamSearchService _search;
    private readonly ITeamJoinRequestService _joinRequests;

    public TeamsController(
        ITeamService teams,
        ITeamActivityService activity,
        ITeamNewsService news,
        ITeamInvitationService invitations,
        ITeamSearchService search,
        ITeamJoinRequestService joinRequests)
    {
        _teams = teams;
        _activity = activity;
        _news = news;
        _invitations = invitations;
        _search = search;
        _joinRequests = joinRequests;
    }

    // --- Browse (public) ------------------------------------------------------

    /// <summary>Anonymous team browse/search (feature 007). Public card fields only; all
    /// filtering/sorting/paging server-side.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<TeamCardDto>>> Browse(
        [FromQuery] TeamBrowseQuery query, [FromQuery] PaginationRequest pagination, CancellationToken ct) =>
        Ok(await _search.BrowseAsync(query, pagination, ct));

    // --- Create & identity ----------------------------------------------------

    [HttpPost]
    public async Task<ActionResult<TeamDetailDto>> Create([FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _teams.CreateAsync(userId, request, ct);
        return result.Status switch
        {
            CreateTeamStatus.Created => Created($"/api/v1/teams/{result.Team!.Slug}", result.Team),
            CreateTeamStatus.SlugTaken => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Team address taken", detail: result.Reason),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid team", detail: result.Reason),
        };
    }

    [HttpGet("slug-available")]
    public async Task<ActionResult<SlugAvailabilityDto>> SlugAvailable([FromQuery] string slug, CancellationToken ct) =>
        Ok(await _teams.CheckSlugAsync(slug, ct));

    [HttpGet("{slug}")]
    public async Task<ActionResult<TeamDetailDto>> GetDetail(string slug, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var dto = await _teams.GetDetailAsync(slug, userId, ct);
        return dto is null ? TeamNotFound() : Ok(dto);
    }

    /// <summary>The public team page (feature 009). Anonymous; optional auth populates the
    /// viewer's relation so members/admins get their extra sections client-side. Public
    /// fields only — never news or contact details.</summary>
    [HttpGet("{slug}/public")]
    [AllowAnonymous]
    public async Task<ActionResult<TeamPublicDetailDto>> GetPublic(string slug, CancellationToken ct)
    {
        var dto = await _teams.GetPublicDetailAsync(slug, GetOptionalUserId(), ct);
        return dto is null ? TeamNotFound() : Ok(dto);
    }

    // --- Join requests (feature 009) ------------------------------------------

    /// <summary>A signed-in non-member asks to join. Idempotent while a request is pending.</summary>
    [HttpPost("{slug}/join-requests")]
    public async Task<IActionResult> RequestToJoin(string slug, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var outcome = await _joinRequests.RequestAsync(slug, userId, ct);
        return outcome switch
        {
            JoinRequestOutcome.Created => NoContent(),
            JoinRequestOutcome.AlreadyPending => NoContent(),
            JoinRequestOutcome.AlreadyMember => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Already a member", detail: "You're already on this team."),
            _ => TeamNotFound(),
        };
    }

    /// <summary>Pending join requests for the team (admin only).</summary>
    [HttpGet("{slug}/join-requests")]
    public async Task<ActionResult<PagedResult<JoinRequestDto>>> GetJoinRequests(
        string slug, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _joinRequests.ListPendingAsync(slug, userId, pagination, ct);
        return result.Gate switch
        {
            JoinQueueGate.Ok => Ok(result.Page),
            JoinQueueGate.Forbidden => Forbidden("Only admins can see join requests."),
            _ => TeamNotFound(),
        };
    }

    [HttpPost("{slug}/join-requests/{requestId:guid}/approve")]
    public Task<IActionResult> ApproveJoinRequest(string slug, Guid requestId, CancellationToken ct) =>
        DecideAsync(slug, requestId, approve: true, ct);

    [HttpPost("{slug}/join-requests/{requestId:guid}/decline")]
    public Task<IActionResult> DeclineJoinRequest(string slug, Guid requestId, CancellationToken ct) =>
        DecideAsync(slug, requestId, approve: false, ct);

    private async Task<IActionResult> DecideAsync(string slug, Guid requestId, bool approve, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var outcome = approve
            ? await _joinRequests.ApproveAsync(slug, requestId, userId, ct)
            : await _joinRequests.DeclineAsync(slug, requestId, userId, ct);
        return outcome switch
        {
            JoinDecisionOutcome.Done => NoContent(),
            JoinDecisionOutcome.Forbidden => Forbidden("Only admins can decide join requests."),
            JoinDecisionOutcome.RequestNotFound => Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Request not found", detail: "No pending request with that id."),
            _ => TeamNotFound(),
        };
    }

    [HttpDelete("{slug}")]
    public async Task<IActionResult> Delete(string slug, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _teams.DeleteAsync(slug, userId, ct);
        return status switch
        {
            DeleteTeamStatus.Deleted => NoContent(),
            DeleteTeamStatus.Forbidden => Forbidden("Only admins can delete the team."),
            _ => TeamNotFound(),
        };
    }

    [HttpPatch("{slug}")]
    public async Task<IActionResult> UpdateSettings(
        string slug, [FromBody] UpdateTeamSettingsRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _teams.UpdateSettingsAsync(slug, userId, request, ct);
        return status switch
        {
            UpdateTeamSettingsStatus.Updated => NoContent(),
            UpdateTeamSettingsStatus.Forbidden => Forbidden("Only admins can change team settings."),
            _ => TeamNotFound(),
        };
    }

    // --- Members --------------------------------------------------------------

    [HttpGet("{slug}/members")]
    public async Task<ActionResult<PagedResult<TeamMemberDto>>> GetMembers(
        string slug, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var page = await _teams.GetRosterAsync(slug, userId, pagination, ct);
        return page is null ? TeamNotFound() : Ok(page);
    }

    [HttpPatch("{slug}/members/{targetUserId:guid}/role")]
    public async Task<IActionResult> SetRole(
        string slug, Guid targetUserId, [FromBody] SetMemberRoleRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _teams.SetRoleAsync(slug, userId, targetUserId, request.Role, ct);
        return MapMemberOp(result, onOk: () => Ok(result.Member));
    }

    [HttpDelete("{slug}/members/{targetUserId:guid}")]
    public async Task<IActionResult> RemoveMember(string slug, Guid targetUserId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _teams.RemoveMemberAsync(slug, userId, targetUserId, ct);
        return MapMemberOp(result, onOk: NoContent);
    }

    [HttpPost("{slug}/members/me/step-down")]
    public async Task<IActionResult> StepDown(string slug, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _teams.StepDownAsync(slug, userId, ct);
        return MapMemberOp(result, onOk: NoContent);
    }

    // --- Activity & news ------------------------------------------------------

    [HttpGet("{slug}/activity")]
    public async Task<ActionResult<PagedResult<Dtos.Profile.ActivityItemDto>>> GetActivity(
        string slug, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var page = await _activity.GetForTeamAsync(slug, userId, pagination, ct);
        return page is null ? TeamNotFound() : Ok(page);
    }

    [HttpGet("{slug}/news")]
    public async Task<ActionResult<PagedResult<TeamNewsDto>>> GetNews(
        string slug, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var page = await _news.GetFeedAsync(slug, userId, pagination, ct);
        return page is null ? TeamNotFound() : Ok(page);
    }

    // --- Invitations (admin) --------------------------------------------------

    [HttpGet("{slug}/invitations")]
    public async Task<IActionResult> GetInvitations(
        string slug, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.ListPendingAsync(slug, userId, pagination, ct);
        return MapAdmin(result.Status, () => Ok(result.Page));
    }

    [HttpPost("{slug}/invitations")]
    public async Task<ActionResult<TeamInvitationDto>> CreateTargetedInvite(
        string slug, [FromBody] CreateTargetedInviteRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.CreateTargetedAsync(slug, userId, request.UserId, ct);
        return result.Status switch
        {
            TargetedInviteStatus.Created => StatusCode(StatusCodes.Status201Created, result.Invitation),
            TargetedInviteStatus.AlreadyInvited => Ok(result.Invitation),
            TargetedInviteStatus.AlreadyMember => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Already a member", detail: "That player is already on the team."),
            TargetedInviteStatus.TargetNotFound => Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Player not found", detail: "No player exists for that account."),
            TargetedInviteStatus.Forbidden => Forbidden("Only admins can invite people."),
            _ => TeamNotFound(),
        };
    }

    [HttpGet("{slug}/invitations/link")]
    public async Task<IActionResult> GetInviteLink(string slug, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.GetActiveLinkAsync(slug, userId, ct);
        return MapAdmin(result.Status, () => result.Link is null ? NoContent() : Ok(result.Link));
    }

    [HttpPost("{slug}/invitations/link")]
    public async Task<IActionResult> RotateInviteLink(string slug, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.CreateOrRotateLinkAsync(slug, userId, ct);
        return MapAdmin(result.Status, () => StatusCode(StatusCodes.Status201Created, result.Link));
    }

    [HttpDelete("{slug}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvite(string slug, Guid invitationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _invitations.RevokeAsync(slug, userId, invitationId, ct);
        return status switch
        {
            RevokeStatus.Revoked => NoContent(),
            RevokeStatus.Forbidden => Forbidden("Only admins can manage invitations."),
            RevokeStatus.NotFound => Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Invitation not found", detail: "No pending invitation with that id."),
            _ => TeamNotFound(),
        };
    }

    [HttpGet("{slug}/invitations/user-search")]
    public async Task<IActionResult> SearchUsers(
        string slug, [FromQuery] string q, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.SearchUsersAsync(slug, userId, q, pagination, ct);
        return MapAdmin(result.Status, () => Ok(result.Page));
    }

    // --- Helpers --------------------------------------------------------------

    private IActionResult MapMemberOp(MemberOpResult result, Func<IActionResult> onOk) => result.Status switch
    {
        MemberOpStatus.Ok => onOk(),
        MemberOpStatus.Forbidden => Forbidden(result.Reason ?? "Only admins can manage members."),
        MemberOpStatus.MemberNotFound => Problem(statusCode: StatusCodes.Status404NotFound,
            title: "Member not found", detail: "That player is not on the team."),
        MemberOpStatus.LastAdmin => Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Last admin", detail: result.Reason),
        _ => TeamNotFound(),
    };

    private IActionResult MapAdmin(InviteAdminStatus status, Func<IActionResult> onOk) => status switch
    {
        InviteAdminStatus.Ok => onOk(),
        InviteAdminStatus.Forbidden => Forbidden("Only admins can manage invitations."),
        _ => TeamNotFound(),
    };

    private ObjectResult TeamNotFound() => Problem(statusCode: StatusCodes.Status404NotFound,
        title: "Team not found", detail: "No team exists at that address, or you're not a member.");

    private ObjectResult Forbidden(string detail) => Problem(statusCode: StatusCodes.Status403Forbidden,
        title: "Forbidden", detail: detail);

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }

    /// <summary>The caller's id when a valid auth cookie is present; null for an anonymous caller.
    /// Used by the public team page to compute the viewer's relation.</summary>
    private Guid? GetOptionalUserId() => TryGetUserId(out var userId) ? userId : null;
}
