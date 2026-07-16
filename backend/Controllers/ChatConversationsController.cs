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

    public ChatConversationsController(IChatConversationService conversations) => _conversations = conversations;

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

    private ObjectResult Fail(ChatOutcome outcome, string? detail) => ChatHttp.Fail(this, outcome, detail);

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
