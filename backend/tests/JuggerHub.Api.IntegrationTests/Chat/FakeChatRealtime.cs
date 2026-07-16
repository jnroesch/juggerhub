using JuggerHub.Dtos.Chat;
using JuggerHub.Services.Chat.Realtime;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Records what the chat services push, so the realtime contract can be asserted without a live
/// socket — which is exactly what the <see cref="IChatRealtime"/> seam exists for (feature 019).
/// </summary>
public sealed class FakeChatRealtime : IChatRealtime
{
    public sealed record MessageCreated(IReadOnlyList<Guid> Recipients, Guid ConversationId, MessageDto Message);
    public sealed record MessageDeleted(IReadOnlyList<Guid> Recipients, Guid ConversationId, Guid MessageId);
    public sealed record UnreadCount(Guid RecipientUserId, int Count);
    public sealed record ConversationUpserted(IReadOnlyList<Guid> Recipients, ConversationSummaryDto Conversation);
    public sealed record Typing(IReadOnlyList<Guid> Recipients, Guid ConversationId, Guid TypistUserId, string DisplayName);

    private readonly object _gate = new();

    public List<MessageCreated> MessagesCreated { get; } = new();
    public List<MessageDeleted> MessagesDeleted { get; } = new();
    public List<UnreadCount> UnreadCounts { get; } = new();
    public List<ConversationUpserted> ConversationsUpserted { get; } = new();
    public List<Typing> Typings { get; } = new();

    /// <summary>Every user id that received any push — the audience, for leak assertions.</summary>
    public IReadOnlyList<Guid> AllRecipients
    {
        get
        {
            lock (_gate)
            {
                return MessagesCreated.SelectMany(m => m.Recipients)
                    .Concat(MessagesDeleted.SelectMany(m => m.Recipients))
                    .Concat(UnreadCounts.Select(u => u.RecipientUserId))
                    .Concat(ConversationsUpserted.SelectMany(c => c.Recipients))
                    .Concat(Typings.SelectMany(t => t.Recipients))
                    .Distinct()
                    .ToList();
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            MessagesCreated.Clear();
            MessagesDeleted.Clear();
            UnreadCounts.Clear();
            ConversationsUpserted.Clear();
            Typings.Clear();
        }
    }

    public Task PushMessageCreatedAsync(IReadOnlyList<Guid> recipientUserIds, Guid conversationId, MessageDto message, CancellationToken ct = default)
    {
        lock (_gate) { MessagesCreated.Add(new MessageCreated(recipientUserIds.ToList(), conversationId, message)); }
        return Task.CompletedTask;
    }

    public Task PushMessageDeletedAsync(IReadOnlyList<Guid> recipientUserIds, Guid conversationId, Guid messageId, CancellationToken ct = default)
    {
        lock (_gate) { MessagesDeleted.Add(new MessageDeleted(recipientUserIds.ToList(), conversationId, messageId)); }
        return Task.CompletedTask;
    }

    public Task PushUnreadCountAsync(Guid recipientUserId, int unreadCount, CancellationToken ct = default)
    {
        lock (_gate) { UnreadCounts.Add(new UnreadCount(recipientUserId, unreadCount)); }
        return Task.CompletedTask;
    }

    public Task PushConversationUpsertedAsync(IReadOnlyList<Guid> recipientUserIds, ConversationSummaryDto conversation, CancellationToken ct = default)
    {
        lock (_gate) { ConversationsUpserted.Add(new ConversationUpserted(recipientUserIds.ToList(), conversation)); }
        return Task.CompletedTask;
    }

    public Task PushTypingAsync(IReadOnlyList<Guid> recipientUserIds, Guid conversationId, Guid typistUserId, string typistDisplayName, CancellationToken ct = default)
    {
        lock (_gate) { Typings.Add(new Typing(recipientUserIds.ToList(), conversationId, typistUserId, typistDisplayName)); }
        return Task.CompletedTask;
    }
}
