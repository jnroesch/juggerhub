using JuggerHub.Dtos.Notifications;
using JuggerHub.Entities;

namespace JuggerHub.Services.Notifications;

/// <summary>
/// Resolves and mutates a user's notification preferences (feature 011). Preferences are sparse and
/// opt-out: a cell with no stored row is <c>enabled</c>. Producers consult
/// <see cref="IsEnabledAsync"/> / <see cref="GetEnabledRecipientsAsync"/> before delivering on a
/// channel; both are fail-safe (default to deliver on error) so a preference hiccup never fails or
/// drops the originating action. All reads/writes are scoped to the caller-supplied user id
/// (resolved from the JWT subject in the controller).
/// </summary>
public interface INotificationPreferenceService
{
    /// <summary>The caller's effective matrix (defaults applied) plus the always-on groups.</summary>
    Task<NotificationPreferenceMatrixDto> GetMatrixAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Upsert one (category, channel) cell for the caller. Idempotent.</summary>
    Task SetCellAsync(Guid userId, NotificationCategory category, NotificationChannel channel, bool enabled, CancellationToken ct = default);

    /// <summary>Whether the user has this channel enabled for this category (default true; fail-safe true on error).</summary>
    Task<bool> IsEnabledAsync(Guid userId, NotificationCategory category, NotificationChannel channel, CancellationToken ct = default);

    /// <summary>Of the given users, those with this (category, channel) enabled — defaults included; fail-safe returns all on error.</summary>
    Task<IReadOnlyCollection<Guid>> GetEnabledRecipientsAsync(
        IReadOnlyCollection<Guid> userIds, NotificationCategory category, NotificationChannel channel, CancellationToken ct = default);
}
