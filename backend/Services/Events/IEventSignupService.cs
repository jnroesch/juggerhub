using JuggerHub.Common;
using JuggerHub.Dtos.Events;
using JuggerHub.Entities;

namespace JuggerHub.Services.Events;

/// <summary>Outcome of a sign-up attempt.</summary>
public enum SignupOutcome
{
    Created,
    NotFound,
    Closed,        // cancelled or already ended
    ModeMismatch,  // individual to teams-only, or team to individuals-only
    NotTeamAdmin,  // caller doesn't administer the entered team
    Duplicate,     // subject already signed up
}

/// <summary>Result of a sign-up (carries the created participant + its routed status, or a reason).</summary>
public sealed record SignupResult(SignupOutcome Outcome, SignupDto? Signup, string? Reason)
{
    public static SignupResult Ok(SignupDto signup) => new(SignupOutcome.Created, signup, null);

    public static SignupResult Fail(SignupOutcome outcome, string? reason = null) => new(outcome, null, reason);
}

/// <summary>Outcome of withdrawing/removing a participant.</summary>
public enum WithdrawStatus
{
    Removed,
    NotFound,
    Forbidden,
}

/// <summary>
/// Sign-up + waitlist workflow for an event: the public group reads (joined / awaiting / waitlist),
/// sign-up (capacity-routed), and withdraw/admin-remove. Occupied spots = Joined + AwaitingApproval;
/// nothing auto-fills. Approve/promote land in later user stories.
/// </summary>
public interface IEventSignupService
{
    /// <summary>List one participant group (public, paginated, arrival order). Null when no event has that id.</summary>
    Task<PagedResult<SignupDto>?> ListGroupAsync(
        Guid eventId, SignupStatus group, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>
    /// Sign up (authenticated). <paramref name="teamId"/> is required for teams-only events (the
    /// caller must administer it) and must be null for individuals-only. Routes by capacity/fee:
    /// free+open→Joined, paid+open→AwaitingApproval, full→Waitlisted (atomically under a row lock).
    /// </summary>
    Task<SignupResult> SignupAsync(Guid eventId, Guid userId, Guid? teamId, CancellationToken ct = default);

    /// <summary>
    /// Withdraw or admin-remove a participant. Allowed for the individual participant, an admin of
    /// the entered team, or an event admin. Releases any held spot and never auto-promotes.
    /// </summary>
    Task<WithdrawStatus> WithdrawAsync(Guid eventId, Guid signupId, Guid actorUserId, CancellationToken ct = default);
}
