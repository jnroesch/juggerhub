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
