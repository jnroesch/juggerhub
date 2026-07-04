using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Events;
using JuggerHub.Entities;
using JuggerHub.Services.Events;
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

    public EventsController(IEventService events, IEventSignupService signups)
    {
        _events = events;
        _signups = signups;
    }

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

    // --- Public reads (anonymous; optional auth populates the viewer relation) ---

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventDetailDto>> GetDetail(Guid id, CancellationToken ct)
    {
        var dto = await _events.GetDetailAsync(id, GetOptionalUserId(), ct);
        return dto is null ? EventNotFound() : Ok(dto);
    }

    [HttpGet("{id:guid}/participants")]
    [AllowAnonymous]
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

    // --- Helpers --------------------------------------------------------------

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
