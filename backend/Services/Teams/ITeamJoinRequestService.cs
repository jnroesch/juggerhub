using JuggerHub.Common;
using JuggerHub.Dtos.Teams;

namespace JuggerHub.Services.Teams;

/// <summary>Outcome of creating a join request.</summary>
public enum JoinRequestOutcome
{
    Created,
    AlreadyPending,
    AlreadyMember,
    TeamNotFound,
}

/// <summary>Outcome of an admin approve/decline.</summary>
public enum JoinDecisionOutcome
{
    Done,
    Forbidden,
    RequestNotFound,
    TeamNotFound,
}

/// <summary>Outcome of a player withdrawing their own pending request.</summary>
public enum JoinCancelOutcome
{
    Cancelled,
    NothingToCancel,
    TeamNotFound,
}

/// <summary>Access gate for the admin request queue.</summary>
public enum JoinQueueGate
{
    Ok,
    Forbidden,
    NotFound,
}

/// <summary>Paged pending requests plus the access gate.</summary>
public sealed record JoinQueueResult(JoinQueueGate Gate, PagedResult<JoinRequestDto>? Page);

/// <summary>
/// The request-to-join workflow (feature 009): a signed-in non-member requests; they can withdraw
/// their own pending request; team admins list pending requests and approve (creating the
/// membership) or decline. Admin actions are guarded server-side by <see cref="TeamMembershipGuard"/>.
/// </summary>
public interface ITeamJoinRequestService
{
    Task<JoinRequestOutcome> RequestAsync(string slug, Guid userId, CancellationToken ct = default);

    /// <summary>The requester withdraws their own pending request. Idempotent: withdrawing when
    /// nothing is pending succeeds as a no-op.</summary>
    Task<JoinCancelOutcome> CancelAsync(string slug, Guid userId, CancellationToken ct = default);

    Task<JoinQueueResult> ListPendingAsync(string slug, Guid adminUserId, PaginationRequest pagination, CancellationToken ct = default);

    Task<JoinDecisionOutcome> ApproveAsync(string slug, Guid requestId, Guid adminUserId, CancellationToken ct = default);

    Task<JoinDecisionOutcome> DeclineAsync(string slug, Guid requestId, Guid adminUserId, CancellationToken ct = default);
}
