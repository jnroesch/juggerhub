using JuggerHub.Common;
using JuggerHub.Dtos.Events;

namespace JuggerHub.Services.Events;

/// <summary>The event's admin list behind an admin gate.</summary>
public sealed record AdminListResult(EventAdminGate Gate, PagedResult<EventAdminDto>? Page);

/// <summary>Outcome of removing/stepping-down an admin.</summary>
public enum AdminOpStatus
{
    Ok,
    NotFound,
    Forbidden,
    AdminNotFound,
    LastAdmin,
}

/// <summary>
/// Event admin roster: list admins and remove/step-down under the last-admin guard (an event
/// always keeps at least one admin), enforced atomically on the event row.
/// </summary>
public interface IEventAdminService
{
    Task<AdminListResult> ListAsync(Guid eventId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Remove an admin, or step down when <paramref name="targetUserId"/> is the caller.</summary>
    Task<AdminOpStatus> RemoveAsync(Guid eventId, Guid actorUserId, Guid targetUserId, CancellationToken ct = default);
}
