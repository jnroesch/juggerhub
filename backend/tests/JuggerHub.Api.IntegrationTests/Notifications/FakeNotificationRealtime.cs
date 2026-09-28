using JuggerHub.Dtos.Notifications;
using JuggerHub.Services.Notifications.Realtime;

namespace JuggerHub.Api.IntegrationTests.Notifications;

/// <summary>
/// Records what the notification engine sends over its realtime channel, so a test can assert
/// both halves of a contract without a live socket: that something reached a recipient (a delete
/// lowering their badge, feature 057) and that nothing did (an edit raising no new alert). Mirrors
/// <c>FakeChatRealtime</c>. Tests share one factory, so read by recipient id — each test's users are
/// its own.
/// </summary>
public sealed class FakeNotificationRealtime : INotificationRealtime
{
    public sealed record Created(Guid RecipientUserId, NotificationDto Notification);

    public sealed record UnreadCount(Guid RecipientUserId, int Count);

    private readonly object _gate = new();
    private readonly List<Created> _created = new();
    private readonly List<UnreadCount> _unreadCounts = new();

    /// <summary>Every "new notification" push to <paramref name="userId"/>, oldest first.</summary>
    public IReadOnlyList<Created> CreatedFor(Guid userId)
    {
        lock (_gate)
        {
            return _created.Where(c => c.RecipientUserId == userId).ToList();
        }
    }

    /// <summary>Every unread count pushed to <paramref name="userId"/>, oldest first.</summary>
    public IReadOnlyList<int> UnreadCountsFor(Guid userId)
    {
        lock (_gate)
        {
            return _unreadCounts.Where(u => u.RecipientUserId == userId).Select(u => u.Count).ToList();
        }
    }

    public Task PushCreatedAsync(Guid recipientUserId, NotificationDto notification, CancellationToken ct = default)
    {
        lock (_gate) { _created.Add(new Created(recipientUserId, notification)); }
        return Task.CompletedTask;
    }

    public Task PushUnreadCountAsync(Guid recipientUserId, int unreadCount, CancellationToken ct = default)
    {
        lock (_gate) { _unreadCounts.Add(new UnreadCount(recipientUserId, unreadCount)); }
        return Task.CompletedTask;
    }
}
