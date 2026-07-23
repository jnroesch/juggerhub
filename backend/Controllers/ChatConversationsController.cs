using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Chat;
using JuggerHub.Services.Chat;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JuggerHub.Security.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace JuggerHub.Controllers;

/// <summary>
/// The chat inbox and conversation surface (feature 019). Thin — <see cref="ChatGuard"/> and the chat
/// services enforce membership server-side, and a conversation the caller is not in is 404 rather than
/// 403 so its existence never leaks.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chat")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class ChatConversationsController : ControllerBase
{
    private readonly IChatConversationService _conversations;
    private readonly IChatSearchService _search;
    private readonly IChatBlockService _blocks;

    public ChatConversationsController(
        IChatConversationService conversations,
        IChatSearchService search,
        IChatBlockService blocks)
    {
        _conversations = conversations;
        _search = search;
        _blocks = blocks;
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<PagedResult<ConversationSummaryDto>>> Inbox(
        [FromQuery] PaginationRequest pagination,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _conversations.GetInboxAsync(userId, pagination, ct));
    }

    [HttpGet("conversations/unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(new UnreadCountDto(await _conversations.GetUnreadTotalAsync(userId, ct)));
    }

    /// <summary>
    /// Start a chat. Rate-limited because direct-message reach is open — any player may message any
    /// other (spec FR-049) — so this is the endpoint that would otherwise let one account open a
    /// conversation with the entire community (spec FR-049a).
    /// </summary>
    [HttpPost("conversations")]
    [EnableRateLimiting(RateLimitPolicies.ChatStart)]
    public async Task<ActionResult<ConversationSummaryDto>> Start(
        [FromBody] CreateConversationRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.StartAsync(userId, request.ParticipantUserIds ?? Array.Empty<Guid>(), request.Name, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    /// <summary>
    /// Send the first message to a player, creating the direct conversation if none exists yet
    /// (feature 022 — lazy DM creation). This is now the DM-creation surface, so it carries the same
    /// open-reach abuse guard as Start (spec FR-049a).
    /// </summary>
    [HttpPost("direct/{targetUserId:guid}/messages")]
    [EnableRateLimiting(RateLimitPolicies.ChatStart)]
    public async Task<ActionResult<DirectMessageSentDto>> SendDirect(
        Guid targetUserId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.SendFirstDirectAsync(userId, targetUserId, request.Body ?? string.Empty, ct);
        return result.IsOk
            ? Created($"/api/v1/chat/conversations/{result.Value!.Conversation.Id}", result.Value)
            : Fail(result.Outcome, result.Error);
    }

    // --- Contact the admins (feature 027) -------------------------------------

    /// <summary>
    /// Send the first message to a team's admins, creating the inquiry thread if none exists yet
    /// (feature 027). Rate-limited like every new-conversation reach (spec FR-013 / 019 FR-049a).
    /// </summary>
    [HttpPost("contact/team/{teamId:guid}/messages")]
    [EnableRateLimiting(RateLimitPolicies.ChatStart)]
    public async Task<ActionResult<InquiryMessageSentDto>> ContactTeam(
        Guid teamId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.SendFirstInquiryAsync(
            userId, Entities.ConversationKind.TeamInquiry, teamId, request.Body ?? string.Empty, ct);
        return result.IsOk
            ? Created($"/api/v1/chat/conversations/{result.Value!.Conversation.Id}", result.Value)
            : Fail(result.Outcome, result.Error);
    }

    /// <summary>Send the first message to an event's admins (feature 027). See <see cref="ContactTeam"/>.</summary>
    [HttpPost("contact/event/{eventId:guid}/messages")]
    [EnableRateLimiting(RateLimitPolicies.ChatStart)]
    public async Task<ActionResult<InquiryMessageSentDto>> ContactEvent(
        Guid eventId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.SendFirstInquiryAsync(
            userId, Entities.ConversationKind.EventInquiry, eventId, request.Body ?? string.Empty, ct);
        return result.IsOk
            ? Created($"/api/v1/chat/conversations/{result.Value!.Conversation.Id}", result.Value)
            : Fail(result.Outcome, result.Error);
    }

    /// <summary>
    /// Whether the caller already has an inquiry thread for this team, so the "Contact admins" button can
    /// jump into it rather than open a fresh compose (FR-004). Never creates anything; reveals only the
    /// caller's own thread.
    /// </summary>
    [HttpGet("contact/team/{teamId:guid}")]
    public async Task<ActionResult<InquiryThreadRefDto>> FindTeamInquiry(Guid teamId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var id = await _conversations.FindInquiryThreadAsync(userId, Entities.ConversationKind.TeamInquiry, teamId, ct);
        return Ok(new InquiryThreadRefDto(id));
    }

    /// <summary>Whether the caller already has an inquiry thread for this event (feature 027).</summary>
    [HttpGet("contact/event/{eventId:guid}")]
    public async Task<ActionResult<InquiryThreadRefDto>> FindEventInquiry(Guid eventId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var id = await _conversations.FindInquiryThreadAsync(userId, Entities.ConversationKind.EventInquiry, eventId, ct);
        return Ok(new InquiryThreadRefDto(id));
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> Detail(Guid conversationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.GetDetailAsync(userId, conversationId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpGet("conversations/{conversationId:guid}/members")]
    public async Task<ActionResult<PagedResult<MemberDto>>> Members(
        Guid conversationId,
        [FromQuery] PaginationRequest pagination,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.GetMembersAsync(userId, conversationId, pagination, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid conversationId,
        [FromBody] MarkReadRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.MarkReadAsync(userId, conversationId, request.LastReadMessageId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    /// <summary>
    /// Signal that the caller is typing. Over REST rather than a hub method, which keeps the hub
    /// push-only and the constitution's "REST is primary" rule intact (research §2). The client
    /// debounces to ~1 call / 3s.
    /// </summary>
    [HttpPost("conversations/{conversationId:guid}/typing")]
    [EnableRateLimiting(RateLimitPolicies.ChatTyping)]
    public async Task<IActionResult> Typing(Guid conversationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.SignalTypingAsync(userId, conversationId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    // --- Group membership (US3) -----------------------------------------------

    [HttpPost("conversations/{conversationId:guid}/members")]
    public async Task<IActionResult> AddMembers(
        Guid conversationId,
        [FromBody] AddMembersRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.AddMembersAsync(userId, conversationId, request.UserIds ?? Array.Empty<Guid>(), ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    [HttpDelete("conversations/{conversationId:guid}/members/me")]
    public async Task<IActionResult> Leave(Guid conversationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.LeaveAsync(userId, conversationId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    // --- Mute / hide (US5) ----------------------------------------------------

    /// <summary>The viewer's own flags. Offered for every kind — on a team/party chat this is what stands in for "leave".</summary>
    [HttpPatch("conversations/{conversationId:guid}/state")]
    public async Task<IActionResult> PatchState(
        Guid conversationId,
        [FromBody] PatchConversationStateRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _conversations.PatchStateAsync(userId, conversationId, request.IsMuted, request.IsHidden, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    // --- Search (US6) ---------------------------------------------------------

    /// <summary>
    /// Search messages and people. A short or missing term returns an empty result rather than a 400 —
    /// the search box calls this on every keystroke, and flashing an error at one character typed would
    /// be its own bug.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ChatSearchResultDto>> Search(
        [FromQuery] string? q,
        [FromQuery] PaginationRequest pagination,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _search.SearchAsync(userId, q ?? string.Empty, pagination, ct));
    }

    // --- Blocks (US5) ---------------------------------------------------------

    [HttpGet("blocks")]
    public async Task<ActionResult<PagedResult<BlockedUserDto>>> Blocks(
        [FromQuery] PaginationRequest pagination,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _blocks.ListAsync(userId, pagination, ct));
    }

    [HttpPost("blocks")]
    public async Task<IActionResult> Block([FromBody] BlockUserRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _blocks.BlockAsync(userId, request.UserId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    [HttpDelete("blocks/{targetUserId:guid}")]
    public async Task<IActionResult> Unblock(Guid targetUserId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _blocks.UnblockAsync(userId, targetUserId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    private ObjectResult Fail(ChatOutcome outcome, string? detail) => ChatHttp.Fail(this, outcome, detail);

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
