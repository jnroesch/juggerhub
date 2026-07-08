using JuggerHub.Dtos.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace JuggerHub.Services.Notifications.Realtime;

/// <summary>
/// <see cref="INotificationRealtime"/> over SignalR. Pushes to the recipient's own
/// <c>user:{id}</c> group (see <see cref="NotificationHub.GroupFor"/>), so only that user's
/// connections receive the event. Event names match the client contract
/// (<c>notificationCreated</c>, <c>unreadCountChanged</c>).
/// </summary>
public sealed class SignalRNotificationRealtime : INotificationRealtime
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationRealtime(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task PushCreatedAsync(Guid recipientUserId, NotificationDto notification, CancellationToken ct = default) =>
        _hub.Clients.Group(NotificationHub.GroupFor(recipientUserId))
            .SendAsync("notificationCreated", notification, ct);

    public Task PushUnreadCountAsync(Guid recipientUserId, int unreadCount, CancellationToken ct = default) =>
        _hub.Clients.Group(NotificationHub.GroupFor(recipientUserId))
            .SendAsync("unreadCountChanged", unreadCount, ct);
}
