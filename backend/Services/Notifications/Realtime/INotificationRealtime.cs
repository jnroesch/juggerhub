using JuggerHub.Dtos.Notifications;

namespace JuggerHub.Services.Notifications.Realtime;

/// <summary>
/// Transport-agnostic push channel for notifications (feature 010). Implemented over SignalR
/// (<see cref="NotificationHub"/>), but the seam lets <see cref="NotificationService"/> stay
/// testable without a live socket and lets a future backplane slot in without touching producers.
/// All pushes are best-effort layered over the durable store — an offline recipient still sees the
/// notification on their next REST load.
/// </summary>
public interface INotificationRealtime
{
    /// <summary>Push a newly created notification to the recipient's connected clients.</summary>
    Task PushCreatedAsync(Guid recipientUserId, NotificationDto notification, CancellationToken ct = default);

    /// <summary>Push the recipient's current unread count so open tabs/devices converge.</summary>
    Task PushUnreadCountAsync(Guid recipientUserId, int unreadCount, CancellationToken ct = default);
}
