using JuggerHub.Dtos.Chat;

namespace JuggerHub.Services.Chat.Realtime;

/// <summary>
/// Transport-agnostic push channel for chat (feature 019). Implemented over SignalR
/// (<see cref="ChatHub"/>), mirroring feature 010's <c>INotificationRealtime</c>.
/// </summary>
/// <remarks>
/// <para>
/// The seam earns its keep twice: it lets the chat services be tested without a live socket, and it
/// is where the Redis backplane slots in without a single producer changing (research §10).
/// </para>
/// <para>
/// <b>Every push here is best-effort, layered over durable storage.</b> Nothing in chat is delivered
/// <em>only</em> over the socket — each event has a REST equivalent that returns the same truth
/// (spec FR-023), so a player whose connection is down is stale, never wrong. Typing is the sole
/// exception, and correctly so: there is no such thing as stale typing.
/// </para>
/// </remarks>
public interface IChatRealtime
{
    /// <summary>Push a new message to a conversation's other members. REST equivalent: GET …/messages.</summary>
    Task PushMessageCreatedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        Guid conversationId,
        MessageDto message,
        CancellationToken ct = default);

    /// <summary>Push a message deletion so open threads swap it for a tombstone. REST equivalent: GET …/messages.</summary>
    Task PushMessageDeletedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        Guid conversationId,
        Guid messageId,
        CancellationToken ct = default);

    /// <summary>Push a recipient's new unread total so their tabs converge. REST equivalent: GET …/unread-count.</summary>
    Task PushUnreadCountAsync(Guid recipientUserId, int unreadCount, CancellationToken ct = default);

    /// <summary>Push a new/updated inbox row. REST equivalent: GET /conversations.</summary>
    Task PushConversationUpsertedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        ConversationSummaryDto conversation,
        CancellationToken ct = default);

    /// <summary>
    /// Push a typing signal to a conversation's other members. Ephemeral by design — it carries its own
    /// expiry, is never persisted, and has no REST equivalent.
    /// </summary>
    Task PushTypingAsync(
        IReadOnlyList<Guid> recipientUserIds,
        Guid conversationId,
        Guid typistUserId,
        string typistDisplayName,
        CancellationToken ct = default);
}
