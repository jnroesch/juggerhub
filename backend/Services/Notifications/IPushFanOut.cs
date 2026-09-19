using JuggerHub.Entities;

namespace JuggerHub.Services.Notifications;

/// <summary>
/// The one seam through which <see cref="NotificationService"/> reaches push (feature 055).
///
/// <para>
/// Deliberately narrow, and deliberately not <c>IPushDispatcher</c> itself: the notification
/// service knows about producer types and payload JSON, while the dispatcher knows about devices
/// and the Web Push protocol. This interface is where one is turned into the other, which keeps
/// composition out of the notification store and delivery out of the composer.
/// </para>
///
/// <para>
/// <b>An implementation must never throw.</b> A push that cannot be composed or delivered must
/// still leave the team news posted, the invitation sent and the training cancelled (spec FR-013).
/// </para>
/// </summary>
public interface IPushFanOut
{
    /// <summary>
    /// Composes the notification for each recipient in their own language and hands it to the
    /// dispatcher. <paramref name="dedupeKey"/> becomes the collapse tag, so a repeat of the same
    /// logical event replaces the first notification rather than stacking a second.
    /// </summary>
    Task FanOutAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        NotificationType type,
        string payloadJson,
        string? dedupeKey,
        CancellationToken ct = default);
}
