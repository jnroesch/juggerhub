using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Dtos.Chat;
using JuggerHub.Services.Chat;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JuggerHub.Common;
using JuggerHub.Security.RateLimiting;
using JuggerHub.Services.Chat.Attachments;
using JuggerHub.Services.Media;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

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
    /// <summary>
    /// Transport ceiling for a send carrying files: the per-file cap times the per-message cap,
    /// plus room for multipart framing and the text. Deliberately generous — the limits that
    /// matter are the per-file and per-count ones, checked below and again in the service. This
    /// one only stops a request that could not possibly be valid from being buffered at all.
    /// </summary>
    private const long MaxUploadRequestBytes =
        ((long)ChatConstants.MaxAttachmentBytes * ChatConstants.MaxAttachmentsPerMessage) + (4 * 1024 * 1024);

    private readonly IChatMessageService _messages;
    private readonly JuggerHub.Services.Chat.Attachments.IChatAttachmentService _attachments;
    private readonly IOptions<MediaStorageOptions> _mediaOptions;

    public ChatMessagesController(
        IChatMessageService messages,
        JuggerHub.Services.Chat.Attachments.IChatAttachmentService attachments,
        IOptions<MediaStorageOptions> mediaOptions)
    {
        _messages = messages;
        _attachments = attachments;
        _mediaOptions = mediaOptions;
    }

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
    [Consumes("application/json")]
    public async Task<ActionResult<MessageDto>> Send(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _messages.SendAsync(userId, conversationId, request.Body ?? string.Empty, ct: ct);
        return result.IsOk
            ? CreatedAtAction(nameof(Page), new { conversationId }, result.Value)
            : Fail(result.Outcome, result.Error);
    }

    /// <summary>
    /// Send a message carrying files (feature 049 / #282). Same route as the JSON send; the
    /// framework picks between them on the request's content type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two actions rather than one that accepts both.</b> The JSON form is what every existing
    /// caller uses and what the realtime paths exercise, and leaving it byte-identical means
    /// attachments cannot regress plain sending. It also keeps the large request-size limit
    /// attached only to the action that can actually need it.
    /// </para>
    /// <para>
    /// <b>Text and files arrive in one request</b>, deliberately. A staged upload followed by a
    /// send would need somewhere to stage, an expiry for abandoned uploads, and a rule stopping
    /// one member attaching another's staged file — three new problems in exchange for avoiding
    /// one large request, and it would make "no partial message" a cleanup path instead of a
    /// property of the transaction (spec FR-008).
    /// </para>
    /// </remarks>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    [EnableRateLimiting(RateLimitPolicies.ChatSend)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    public async Task<ActionResult<MessageDto>> SendWithFiles(
        Guid conversationId,
        [FromForm] string? body,
        [FromForm] List<IFormFile>? files,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var selected = files ?? [];

        // Count first, before a single byte is read into memory. The service checks it again — it
        // is the boundary and cannot rely on a caller — but refusing here means an over-count
        // request costs nothing rather than costing ten buffered files.
        if (selected.Count > ChatConstants.MaxAttachmentsPerMessage)
        {
            return Fail(
                ChatOutcome.Invalid,
                $"You can send up to {ChatConstants.MaxAttachmentsPerMessage} files at once.");
        }

        var uploads = new List<JuggerHub.Services.Chat.Attachments.ChatUploadFile>(selected.Count);
        foreach (var file in selected)
        {
            // Same reason: an over-size file is refused on its declared length before it is read.
            // The service re-checks the real length, because a declared length is the client
            // talking (Principle I).
            if (file.Length > ChatConstants.MaxAttachmentBytes)
            {
                return Fail(
                    ChatOutcome.Invalid,
                    $"{file.FileName} is too large. Files can be up to " +
                    $"{ChatConstants.MaxAttachmentBytes / (1024 * 1024)} MB.");
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            uploads.Add(new JuggerHub.Services.Chat.Attachments.ChatUploadFile(file.FileName, buffer.ToArray()));
        }

        var result = await _messages.SendAsync(userId, conversationId, body ?? string.Empty, uploads, ct);
        return result.IsOk
            ? CreatedAtAction(nameof(Page), new { conversationId }, result.Value)
            : Fail(result.Outcome, result.Error);
    }

    /// <summary>
    /// The bytes of one attachment (feature 049 / #282).
    /// </summary>
    /// <remarks>
    /// <b>404 for every refusal</b> — absent, not permitted, withdrawn, and unreadable are
    /// deliberately indistinguishable, so this never becomes a way to discover which attachment
    /// ids exist (spec FR-025). The same reasoning the avatar endpoint already applies.
    /// </remarks>
    [HttpGet("attachments/{attachmentId:guid}")]
    [EnableRateLimiting(RateLimitPolicies.MediaRead)]
    public async Task<IActionResult> Attachment(Guid attachmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var content = await _attachments.OpenAsync(attachmentId, userId, ct);
        if (content is not { } file)
        {
            return NotFound();
        }

        // Inline for a normalized image, which is ours and renders as the preview in the thread;
        // a download for everything else, because a member-supplied file must never be rendered in
        // this origin (see MediaResponse, and the absent CSP header it explains).
        var downloadName = AttachmentContentType.IsImage(file.ContentType) ? null : file.FileName;

        return MediaResponse.File(
            this,
            new MediaContent(new MemoryStream(file.Content, writable: false), file.ContentType, file.ObjectKey),
            _mediaOptions.Value,
            downloadName);
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
