using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Dtos.Chat;
using JuggerHub.Services.Chat;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JuggerHub.Security.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace JuggerHub.Controllers;

/// <summary>
/// Messages within a conversation (feature 019). Thin — the message service authorizes membership and
/// sender-ownership server-side.
/// </summary>
/// <remarks>
/// There is deliberately <b>no PATCH/PUT</b> here: a sent message is immutable, and the only
/// correction available is delete-and-resend (spec FR-050b).
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chat")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class ChatMessagesController : ControllerBase
{
    private readonly IChatMessageService _messages;

    public ChatMessagesController(IChatMessageService messages) => _messages = messages;

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<MessagePageDto>> Page(
        Guid conversationId,
        [FromQuery] Guid? before,
        [FromQuery] int take = ChatConstants.MessagePageSize,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _messages.GetPageAsync(userId, conversationId, before, take, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    [EnableRateLimiting(RateLimitPolicies.ChatSend)]
    public async Task<ActionResult<MessageDto>> Send(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _messages.SendAsync(userId, conversationId, request.Body ?? string.Empty, ct);
        return result.IsOk
            ? CreatedAtAction(nameof(Page), new { conversationId }, result.Value)
            : Fail(result.Outcome, result.Error);
    }

    [HttpDelete("messages/{messageId:guid}")]
    public async Task<IActionResult> Delete(Guid messageId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _messages.DeleteAsync(userId, messageId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    private ObjectResult Fail(ChatOutcome outcome, string? detail) => ChatHttp.Fail(this, outcome, detail);

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
