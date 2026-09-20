namespace JuggerHub.Services.Notifications.Push;

/// <summary>
/// What a member reads on their lock screen, composed on the server (feature 055).
/// </summary>
/// <param name="Title">Short heading, already localized to the recipient's language.</param>
/// <param name="Body">What happened and what it concerns, already localized.</param>
/// <param name="Url">
/// App-relative path the notification opens, always beginning with <c>/</c>. Never absolute and
/// never built from member input — the service worker refuses anything else.
/// </param>
/// <param name="Tag">
/// Collapse key. A second arrival with the same tag REPLACES the first on the device instead of
/// stacking, and it is also sent as the push message's topic so a push service replaces a message
/// it still holds undelivered. This is how once-only delivery is achieved without a dedupe store.
/// </param>
public sealed record PushContent(string Title, string Body, string Url, string Tag);

/// <summary>
/// Delivers push notifications to members' enabled devices (feature 055).
///
/// <para>
/// <b>This is called BY <see cref="NotificationService"/>, and is deliberately not implemented
/// inside it.</b> Chat writes no notification rows by design (feature 019, FR-051a), so if push
/// lived inside the notification store, chat push (#309) would either be impossible or would have
/// to reverse that decision. A seam below the store lets <c>ChatMessageService</c> call this
/// directly, writing nothing to <c>Notifications</c>.
/// </para>
///
/// <para>
/// Implementations must never let a failure reach the caller's caller: the action that produced the
/// notification succeeds whether or not the notification leaves the building.
/// </para>
/// </summary>
public interface IPushDispatcher
{
    /// <summary>
    /// Delivers <paramref name="content"/> to every enabled device of every recipient. Recipients
    /// with no enabled device cost nothing — no outbound call is made for them.
    /// </summary>
    Task DispatchAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        PushContent content,
        CancellationToken ct = default);
}
