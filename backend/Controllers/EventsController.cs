using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Events;
using JuggerHub.Dtos.Parties;
using JuggerHub.Dtos.Search;
using JuggerHub.Entities;
using JuggerHub.Services.Events;
using JuggerHub.Services.Parties;
using JuggerHub.Services.Search;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Events. The event page and its lists are <b>public</b> (a visitor decides before signing in);
/// creating and signing up require authentication; every admin action is gated on
/// <c>EventAdmin</c> membership server-side (constitution Principle I).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/events")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class EventsController : ControllerBase
{
    private readonly IEventService _events;
    private readonly IEventSignupService _signups;
    private readonly IEventNewsService _news;
    private readonly IEventContactService _contacts;
    private readonly IEventAdminService _admins;
    private readonly IEventInvitationService _invitations;
    private readonly IEventSearchService _search;
    private readonly IPartyService _parties;

    public EventsController(
        IEventService events,
        IEventSignupService signups,
        IEventNewsService news,
        IEventContactService contacts,
        IEventAdminService admins,
        IEventInvitationService invitations,
        IEventSearchService search,
        IPartyService parties)
    {
        _events = events;
        _signups = signups;
        _news = news;
        _contacts = contacts;
        _admins = admins;
        _invitations = invitations;
        _search = search;
        _parties = parties;
    }

    /// <summary>The signed-in caller's party affordances for a teams-only event (feature 016):
    /// the teams they administer and whether each already has a party.</summary>
    [HttpGet("{id:guid}/party-context")]
    public async Task<ActionResult<PartyContextDto>> PartyContext(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var dto = await _parties.GetContextAsync(id, userId, ct);
        return dto is null ? EventNotFound() : Ok(dto);
    }

    // --- Browse (public) ------------------------------------------------------

    /// <summary>Event browse/search (feature 007; authenticated-only since feature 026).
    /// Cancelled events are always excluded; past events hidden by default. Public card
    /// fields only.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<EventCardDto>>> Browse(
        [FromQuery] EventBrowseQuery query, [FromQuery] PaginationRequest pagination, CancellationToken ct) =>
        Ok(await _search.BrowseAsync(query, pagination, ct));

    // --- Create ---------------------------------------------------------------

    [HttpPost]
    public async Task<ActionResult<EventDetailDto>> Create([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _events.CreateAsync(userId, request, ct);
        return result.Status switch
        {
            CreateEventStatus.Created => Created($"/api/v1/events/{result.Event!.Id}", result.Event),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid event", detail: result.Reason),
        };
    }

    // --- Reads (authenticated since feature 026; auth populates the viewer relation) ---

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EventDetailDto>> GetDetail(Guid id, CancellationToken ct)
    {
        var dto = await _events.GetDetailAsync(id, GetOptionalUserId(), ct);
        return dto is null ? EventNotFound() : Ok(dto);
    }

    [HttpGet("{id:guid}/participants")]
    public async Task<ActionResult<PagedResult<SignupDto>>> GetParticipants(
        Guid id, [FromQuery] string group, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryParseGroup(group, out var status))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid group",
                detail: "group must be one of: joined, awaiting, waitlist.");
        }

        var page = await _signups.ListGroupAsync(id, status, pagination, ct);
        return page is null ? EventNotFound() : Ok(page);
    }

    // --- Sign-up & withdraw ---------------------------------------------------

    [HttpPost("{id:guid}/signup")]
    public async Task<ActionResult<SignupDto>> Signup(Guid id, [FromBody] SignupRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _signups.SignupAsync(id, userId, request?.TeamId, ct);
        return result.Outcome switch
        {
            SignupOutcome.Created => Created($"/api/v1/events/{id}", result.Signup),
            SignupOutcome.NotFound => EventNotFound(),
            SignupOutcome.NotTeamAdmin => Forbidden(result.Reason ?? "Not allowed."),
            SignupOutcome.ModeMismatch => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Wrong sign-up type", detail: result.Reason),
            SignupOutcome.Closed => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Sign-ups closed", detail: result.Reason),
            SignupOutcome.Duplicate => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Already signed up", detail: result.Reason),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Sign-up failed", detail: result.Reason),
        };
    }

    [HttpDelete("{id:guid}/signup/{signupId:guid}")]
    public async Task<IActionResult> Withdraw(Guid id, Guid signupId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _signups.WithdrawAsync(id, signupId, userId, ct);
        return status switch
        {
            WithdrawStatus.Removed => NoContent(),
            WithdrawStatus.Forbidden => Forbidden("You can't remove this participant."),
            _ => EventNotFound(),
        };
    }

    // --- Admin: edit + participant administration -----------------------------

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<EventDetailDto>> Edit(Guid id, [FromBody] EditEventRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _events.EditAsync(id, userId, request, ct);
        return result.Status switch
        {
            EditEventStatus.Updated => Ok(result.Event),
            EditEventStatus.NotFound => EventNotFound(),
            EditEventStatus.Forbidden => Forbidden(result.Reason ?? "Not allowed."),
            EditEventStatus.LimitBelowOccupied => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Limit too low", detail: result.Reason),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid event", detail: result.Reason),
        };
    }

    [HttpPost("{id:guid}/participants/{signupId:guid}/approve")]
    public async Task<ActionResult<SignupDto>> Approve(Guid id, Guid signupId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return MapAdmit(await _signups.ApproveAsync(id, signupId, userId, ct));
    }

    [HttpPost("{id:guid}/participants/{signupId:guid}/promote")]
    public async Task<ActionResult<SignupDto>> Promote(Guid id, Guid signupId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return MapAdmit(await _signups.PromoteAsync(id, signupId, userId, ct));
    }

    // --- News (authenticated read, admin post) --------------------------------

    [HttpGet("{id:guid}/news")]
    public async Task<ActionResult<PagedResult<EventNewsDto>>> GetNews(
        Guid id, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        var page = await _news.GetFeedAsync(id, pagination, ct);
        return page is null ? EventNotFound() : Ok(page);
    }

    [HttpPost("{id:guid}/news")]
    public async Task<ActionResult<EventNewsDto>> PostNews(Guid id, [FromBody] CreateNewsRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _news.PostAsync(id, userId, request.Body, ct);
        return result.Status switch
        {
            PostNewsStatus.Posted => Created($"/api/v1/events/{id}/news", result.Post),
            PostNewsStatus.Forbidden => Forbidden("Only an event admin can post news."),
            _ => EventNotFound(),
        };
    }

    // --- Contacts (authenticated read, admin CUD) -----------------------------

    [HttpGet("{id:guid}/contacts")]
    public async Task<ActionResult<PagedResult<EventContactDto>>> GetContacts(
        Guid id, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        var page = await _contacts.ListAsync(id, pagination, ct);
        return page is null ? EventNotFound() : Ok(page);
    }

    [HttpPost("{id:guid}/contacts")]
    public async Task<ActionResult<EventContactDto>> AddContact(Guid id, [FromBody] CreateContactRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return MapContact(await _contacts.AddAsync(id, userId, request, ct), created: $"/api/v1/events/{id}/contacts");
    }

    [HttpPatch("{id:guid}/contacts/{contactId:guid}")]
    public async Task<ActionResult<EventContactDto>> UpdateContact(
        Guid id, Guid contactId, [FromBody] CreateContactRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return MapContact(await _contacts.UpdateAsync(id, contactId, userId, request, ct), created: null);
    }

    [HttpDelete("{id:guid}/contacts/{contactId:guid}")]
    public async Task<IActionResult> DeleteContact(Guid id, Guid contactId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _contacts.RemoveAsync(id, contactId, userId, ct);
        return status switch
        {
            ContactOpStatus.Ok => NoContent(),
            ContactOpStatus.Forbidden => Forbidden("Only an event admin can manage contacts."),
            ContactOpStatus.NotFound => EventNotFound(),
            _ => Problem(statusCode: StatusCodes.Status404NotFound, title: "Contact not found", detail: "No such contact."),
        };
    }

    // --- Cancel ---------------------------------------------------------------

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _events.CancelAsync(id, userId, ct);
        return status switch
        {
            CancelEventStatus.Cancelled => NoContent(),
            CancelEventStatus.Forbidden => Forbidden("Only an event admin can cancel it."),
            CancelEventStatus.AlreadyCancelled => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Already cancelled", detail: "This event is already cancelled."),
            _ => EventNotFound(),
        };
    }

    // --- Admins ---------------------------------------------------------------

    [HttpGet("{id:guid}/admins")]
    public async Task<ActionResult<PagedResult<EventAdminDto>>> GetAdmins(
        Guid id, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _admins.ListAsync(id, userId, pagination, ct);
        return result.Gate switch
        {
            EventAdminGate.Ok => Ok(result.Page),
            EventAdminGate.Forbidden => Forbidden("Only an event admin can view the admin list."),
            _ => EventNotFound(),
        };
    }

    [HttpDelete("{id:guid}/admins/{targetUserId:guid}")]
    public async Task<IActionResult> RemoveAdmin(Guid id, Guid targetUserId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _admins.RemoveAsync(id, userId, targetUserId, ct);
        return status switch
        {
            AdminOpStatus.Ok => NoContent(),
            AdminOpStatus.Forbidden => Forbidden("Only an event admin can manage admins."),
            AdminOpStatus.LastAdmin => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Last admin", detail: "Appoint another admin before removing the last one."),
            AdminOpStatus.AdminNotFound => Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Admin not found", detail: "That user isn't an admin of this event."),
            _ => EventNotFound(),
        };
    }

    // --- Co-admin invitations (admin) -----------------------------------------

    [HttpGet("{id:guid}/invitations/link")]
    public async Task<ActionResult<EventInviteLinkDto>> GetInviteLink(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return MapLink(await _invitations.GetActiveLinkAsync(id, userId, ct));
    }

    [HttpPost("{id:guid}/invitations/link")]
    public async Task<ActionResult<EventInviteLinkDto>> RotateInviteLink(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return MapLink(await _invitations.CreateOrRotateLinkAsync(id, userId, ct));
    }

    [HttpGet("{id:guid}/invitations")]
    public async Task<ActionResult<PagedResult<EventInvitationDto>>> GetInvitations(
        Guid id, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.ListPendingAsync(id, userId, pagination, ct);
        return result.Gate switch
        {
            EventAdminGate.Ok => Ok(result.Page),
            EventAdminGate.Forbidden => Forbidden("Only an event admin can view invitations."),
            _ => EventNotFound(),
        };
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<EventInvitationDto>> CreateInvitation(
        Guid id, [FromBody] CreateEventInviteRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.CreateTargetedAsync(id, userId, request.UserId, ct);
        return result.Outcome switch
        {
            TargetedInviteOutcome.Created => Created($"/api/v1/events/{id}/invitations", result.Invitation),
            TargetedInviteOutcome.AlreadyInvited => Ok(result.Invitation),
            TargetedInviteOutcome.AlreadyAdmin => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Already an admin", detail: "That user already administers this event."),
            TargetedInviteOutcome.TargetNotFound => Problem(statusCode: StatusCodes.Status404NotFound,
                title: "User not found", detail: "No such user."),
            TargetedInviteOutcome.Forbidden => Forbidden("Only an event admin can invite co-admins."),
            _ => EventNotFound(),
        };
    }

    [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid id, Guid invitationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var outcome = await _invitations.RevokeAsync(id, userId, invitationId, ct);
        return outcome switch
        {
            RevokeOutcome.Revoked => NoContent(),
            RevokeOutcome.Forbidden => Forbidden("Only an event admin can revoke invitations."),
            RevokeOutcome.InviteNotFound => Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Invitation not found", detail: "No pending invitation with that id."),
            _ => EventNotFound(),
        };
    }

    [HttpGet("{id:guid}/invitations/user-search")]
    public async Task<ActionResult<PagedResult<EventInvitableUserDto>>> SearchUsers(
        Guid id, [FromQuery] string query, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _invitations.SearchUsersAsync(id, userId, query, pagination, ct);
        return result.Gate switch
        {
            EventAdminGate.Ok => Ok(result.Page),
            EventAdminGate.Forbidden => Forbidden("Only an event admin can search users."),
            _ => EventNotFound(),
        };
    }

    // --- Helpers --------------------------------------------------------------

    private ActionResult<EventInviteLinkDto> MapLink(EventInviteLinkResult result) => result.Gate switch
    {
        EventAdminGate.Ok => Ok(result.Link),
        EventAdminGate.Forbidden => Forbidden("Only an event admin can manage the invite link."),
        _ => EventNotFound(),
    };

    private ActionResult<EventContactDto> MapContact(ContactOpResult result, string? created) => result.Status switch
    {
        ContactOpStatus.Ok when created is not null => Created(created, result.Contact),
        ContactOpStatus.Ok => Ok(result.Contact),
        ContactOpStatus.Forbidden => Forbidden(result.Reason ?? "Not allowed."),
        ContactOpStatus.Invalid => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid contact", detail: result.Reason),
        ContactOpStatus.ContactNotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Contact not found", detail: "No such contact."),
        _ => EventNotFound(),
    };

    private ActionResult<SignupDto> MapAdmit(AdmitResult result) => result.Outcome switch
    {
        AdmitOutcome.Ok => Ok(result.Signup),
        AdmitOutcome.NotFound => EventNotFound(),
        AdmitOutcome.Forbidden => Forbidden(result.Reason ?? "Not allowed."),
        AdmitOutcome.CapacityExceeded => Problem(statusCode: StatusCodes.Status409Conflict,
            title: "No open spot", detail: result.Reason),
        AdmitOutcome.EventClosed => Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Event closed", detail: result.Reason),
        _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "Not applicable", detail: result.Reason),
    };

    private static bool TryParseGroup(string? group, out SignupStatus status)
    {
        switch ((group ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "joined": status = SignupStatus.Joined; return true;
            case "awaiting": status = SignupStatus.AwaitingApproval; return true;
            case "waitlist": status = SignupStatus.Waitlisted; return true;
            default: status = default; return false;
        }
    }

    private ActionResult EventNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Event not found",
            detail: "No event matches that address.");

    private ObjectResult Forbidden(string detail) =>
        Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail);

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }

    /// <summary>The caller's id when a valid auth cookie is present; null for an anonymous caller.</summary>
    private Guid? GetOptionalUserId() => TryGetUserId(out var userId) ? userId : null;
}
