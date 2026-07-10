using JuggerHub.Common;
using JuggerHub.Dtos.Admin;
using JuggerHub.Entities;

namespace JuggerHub.Services.Admin;

/// <summary>Outcome of an administrative account action (feature 013 US4).</summary>
public enum AdminUserActionOutcome
{
    Done,

    /// <summary>No player matches that handle.</summary>
    NotFound,

    /// <summary>The account is not in a state this action applies to (idempotence guard).</summary>
    InvalidTransition,

    /// <summary>
    /// The target is a platform administrator (or the caller themselves — always an
    /// admin): suspend/ban is refused until they are removed from the admin
    /// configuration (spec FR-019).
    /// </summary>
    ProtectedAdmin,
}

/// <summary>
/// Admin user management (feature 013 US3/US4): search over ALL accounts (including
/// banned), the per-player detail, and the recorded, reversible account actions.
/// </summary>
public interface IAdminUserService
{
    Task<PagedResult<AdminUserListItemDto>> SearchAsync(
        string? q, AccountStatus? status, PaginationRequest pagination, CancellationToken ct = default);

    Task<AdminUserDetailDto?> GetDetailAsync(string handle, CancellationToken ct = default);

    Task<AdminUserActionOutcome> SuspendAsync(Guid actorId, string handle, CancellationToken ct = default);

    Task<AdminUserActionOutcome> ReinstateAsync(Guid actorId, string handle, CancellationToken ct = default);

    Task<AdminUserActionOutcome> BanAsync(Guid actorId, string handle, CancellationToken ct = default);

    Task<AdminUserActionOutcome> UnbanAsync(Guid actorId, string handle, CancellationToken ct = default);

    Task<AdminUserActionOutcome> SendPasswordResetAsync(Guid actorId, string handle, CancellationToken ct = default);
}
