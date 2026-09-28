using JuggerHub.Common;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Entities;

namespace JuggerHub.Services.Notifications;

/// <summary>
/// The reusable in-app notification engine (feature 010). Producers call <see cref="CreateAsync"/>
/// / <see cref="CreateManyAsync"/> to notify recipients; the surface reads via
/// <see cref="ListAsync"/> / <see cref="CountUnreadAsync"/> and mutates via the mark-read methods.
/// Every read and mutation is scoped to the recipient by the caller-supplied user id (resolved
/// from the JWT subject in the controller) — the service never trusts a client-supplied recipient.
/// Create is resilient: a delivery failure must not propagate into the producer's own action.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Create one notification for <paramref name="recipientUserId"/> and push it in real time.
    /// <paramref name="payload"/> is serialized to the row's JSON payload. When
    /// <paramref name="dedupeKey"/> is supplied, a duplicate for the same (recipient, key) is
    /// silently ignored (idempotency). Never throws for a delivery/push problem.
    /// </summary>
    Task CreateAsync(
        Guid recipientUserId,
        NotificationType type,
        object payload,
        Guid? actorUserId = null,
        string? dedupeKey = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fan out one notification (same type + payload) to many recipients — e.g. team news to a
    /// roster. Skips duplicates by <paramref name="dedupeKeyPrefix"/>+recipient when supplied.
    /// </summary>
    Task CreateManyAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        NotificationType type,
        object payload,
        Guid? actorUserId = null,
        string? dedupeKeyPrefix = null,
        CancellationToken ct = default);

    /// <summary>
    /// Rewrite the payload of every notification of <paramref name="type"/> that
    /// <see cref="CreateManyAsync"/> wrote under <paramref name="dedupeKeyPrefix"/> — the same prefix
    /// string the producer passed there (feature 057: an edited team news post corrects the excerpt
    /// its alerts carry). Read state, inbox order and every timestamp but <c>ModifiedDate</c> are
    /// left alone, so a corrected row is not a new alert. Pushes nothing, which makes it safe inside
    /// the caller's transaction. Returns the number of rows rewritten.
    /// </summary>
    Task<int> ReplacePayloadAsync(
        NotificationType type,
        string dedupeKeyPrefix,
        object payload,
        CancellationToken ct = default);

    /// <summary>
    /// A team was renamed (feature 061): write <paramref name="teamName"/> into every notification
    /// whose payload names the team with the slug <paramref name="teamSlug"/>, whoever received it —
    /// people who have since left the team included, since rows are found by the team and never
    /// through its roster. Only the payload's <c>teamName</c> and <c>ModifiedDate</c> change: read
    /// state, inbox order and every other key are left alone, and nothing is pushed, so it is silent
    /// and safe inside the caller's transaction. Returns the number of rows rewritten.
    /// </summary>
    Task<int> ReplaceTeamNameAsync(
        string teamSlug,
        string teamName,
        CancellationToken ct = default);

    /// <summary>
    /// Delete every notification of <paramref name="type"/> that <see cref="CreateManyAsync"/> wrote
    /// under <paramref name="dedupeKeyPrefix"/> — every recipient's, including people no longer
    /// connected to the source (feature 057: a deleted team news post takes its alerts with it).
    /// Pushes nothing, which makes it safe inside the caller's transaction; returns the recipients who
    /// lost an <em>unread</em> row, so the caller can <see cref="RefreshUnreadBadgesAsync"/> once it
    /// has committed.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> DeleteManyAsync(
        NotificationType type,
        string dedupeKeyPrefix,
        CancellationToken ct = default);

    /// <summary>
    /// Send each recipient's current unread count to their open clients, so a badge that a deletion
    /// lowered drops without a reload. Best-effort: a delivery failure is logged, never thrown. Call
    /// it only after the change it reflects has committed.
    /// </summary>
    Task RefreshUnreadBadgesAsync(IReadOnlyCollection<Guid> recipientUserIds, CancellationToken ct = default);

    /// <summary>The recipient's notifications, newest-first, paginated. Never unbounded.</summary>
    Task<PagedResult<NotificationDto>> ListAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The recipient's current unread count (the bell badge).</summary>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Mark one notification read. Idempotent. Returns false only when the id is not the caller's
    /// notification (so the controller can 404 without leaking existence).
    /// </summary>
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    /// <summary>Mark all the caller's unread notifications read. Returns the number affected.</summary>
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
