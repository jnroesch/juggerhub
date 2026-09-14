using JuggerHub.Dtos.Chat;
using JuggerHub.Entities;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Messages: sending, reading history, and a sender withdrawing their own message (feature 019).
/// </summary>
public interface IChatMessageService
{
    /// <summary>
    /// Send a message to a conversation the caller is a member of: text, files, or both.
    /// </summary>
    /// <remarks>
    /// <paramref name="files"/> defaults to none, so every existing text-only caller is unchanged.
    /// A message needs text <em>or</em> at least one file — but not both, because a photo is a
    /// perfectly good thing to say on its own (feature 049, spec FR-004).
    /// </remarks>
    Task<ChatResult<MessageDto>> SendAsync(
        Guid callerId,
        Guid conversationId,
        string body,
        IReadOnlyList<Attachments.ChatUploadFile>? files = null,
        CancellationToken ct = default);

    /// <summary>
    /// A keyset page of history, newest first. <paramref name="before"/> is the exclusive upper-bound
    /// message id (null = start at the newest).
    /// </summary>
    Task<ChatResult<MessagePageDto>> GetPageAsync(
        Guid callerId,
        Guid conversationId,
        Guid? before,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Delete the caller's own message. Anyone else — including other members — gets
    /// <see cref="ChatOutcome.Forbidden"/>; there is no moderator delete (spec FR-050a).
    /// </summary>
    Task<ChatResult> DeleteAsync(Guid callerId, Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Write a quiet system line ("Nia B. joined the team"). Internal — no controller exposes this;
    /// it is called by the conversation service and by the team/party roster services.
    /// </summary>
    Task WriteSystemMessageAsync(
        Guid conversationId,
        ChatSystemEvent systemEvent,
        Guid subjectUserId,
        CancellationToken ct = default);
}
