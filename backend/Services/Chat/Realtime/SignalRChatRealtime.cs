using JuggerHub.Dtos.Chat;
using JuggerHub.Services.Chat;
using Microsoft.AspNetCore.SignalR;

namespace JuggerHub.Services.Chat.Realtime;

/// <summary>
/// <see cref="IChatRealtime"/> over SignalR. Pushes to each recipient's own <c>user:{id}</c> group
/// (see <see cref="ChatHub.GroupFor"/>), so only that user's connections receive the event. Event
/// names match the client contract (see specs/019-chat/contracts/chat-api.md).
/// </summary>
/// <remarks>
/// The recipient list is always resolved <b>server-side</b> by <see cref="ChatGuard"/> before it
/// reaches this class — a client never names its own audience (spec FR-022). Across replicas the Redis
/// backplane relays each push to whichever pod holds the recipient's connection (research §10).
/// </remarks>
public sealed class SignalRChatRealtime : IChatRealtime
{
    private readonly IHubContext<ChatHub> _hub;

    public SignalRChatRealtime(IHubContext<ChatHub> hub) => _hub = hub;

    public Task PushMessageCreatedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        Guid conversationId,
        MessageDto message,
        CancellationToken ct = default) =>
        _hub.Clients.Groups(Groups(recipientUserIds))
            .SendAsync("chatMessageCreated", new { conversationId, message }, ct);

    public Task PushMessageDeletedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        Guid conversationId,
        Guid messageId,
        CancellationToken ct = default) =>
        _hub.Clients.Groups(Groups(recipientUserIds))
            .SendAsync("chatMessageDeleted", new { conversationId, messageId }, ct);

    public Task PushUnreadCountAsync(Guid recipientUserId, int unreadCount, CancellationToken ct = default) =>
        _hub.Clients.Group(ChatHub.GroupFor(recipientUserId))
            .SendAsync("chatUnreadCountChanged", new { unreadCount }, ct);

    public Task PushConversationUpsertedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        ConversationSummaryDto conversation,
        CancellationToken ct = default) =>
        _hub.Clients.Groups(Groups(recipientUserIds))
            .SendAsync("chatConversationUpserted", new { conversation }, ct);

    public Task PushTypingAsync(
        IReadOnlyList<Guid> recipientUserIds,
        Guid conversationId,
        Guid typistUserId,
        string typistDisplayName,
        CancellationToken ct = default) =>
        _hub.Clients.Groups(Groups(recipientUserIds))
            .SendAsync(
                "chatTyping",
                new
                {
                    conversationId,
                    userId = typistUserId,
                    displayName = typistDisplayName,
                    expiresInMs = ChatConstants.TypingExpirySeconds * 1000,
                },
                ct);

    private static IReadOnlyList<string> Groups(IReadOnlyList<Guid> userIds) =>
        userIds.Select(ChatHub.GroupFor).ToList();
}
