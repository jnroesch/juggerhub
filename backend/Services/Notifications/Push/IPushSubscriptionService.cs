namespace JuggerHub.Services.Notifications.Push;

/// <summary>
/// The device registrations a member has enabled notifications on (feature 055).
/// </summary>
public interface IPushSubscriptionService
{
    /// <summary>
    /// Registers the browser identified by <paramref name="endpoint"/> against
    /// <paramref name="userId"/>, or does nothing if it is already registered to them.
    /// </summary>
    /// <remarks>
    /// An endpoint already held by a DIFFERENT account is <b>moved</b> to this one. A push endpoint
    /// identifies a browser, not a person, so this is the shared-device case: without the move the
    /// previous owner would keep receiving the new owner's notifications. The unique index on the
    /// endpoint is what makes that correct by construction rather than by cleanup.
    /// </remarks>
    Task RegisterAsync(
        Guid userId,
        string endpoint,
        string p256dh,
        string auth,
        string? deviceLabel,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the caller's registration for <paramref name="endpoint"/>. Idempotent, and scoped to
    /// the caller: an endpoint belonging to somebody else is a no-op and is never acknowledged as
    /// existing.
    /// </summary>
    Task RemoveAsync(Guid userId, string endpoint, CancellationToken ct = default);
}
