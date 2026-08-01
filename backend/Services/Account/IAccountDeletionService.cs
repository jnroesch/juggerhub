using JuggerHub.Dtos.Account;

namespace JuggerHub.Services.Account;

/// <summary>Why an erasure attempt ended the way it did.</summary>
public enum AccountDeletionOutcome
{
    /// <summary>Erased. Complete, immediate, irreversible (spec FR-036, FR-029).</summary>
    Done,

    /// <summary>The confirmation literal was missing or not one of the accepted values (FR-004).</summary>
    ConfirmationMismatch,

    /// <summary>
    /// The password did not verify, or Identity lockout is engaged. Deliberately one outcome for
    /// both: distinguishing them would tell an attacker which state they are in (Principle I).
    /// </summary>
    PasswordRejected,

    /// <summary>
    /// Suspended, banned, or already erased. Refused server-side rather than via the sign-in gate
    /// (FR-005) — this is what keeps erasure from becoming a way to shed moderation, and what makes
    /// releasing the email address safe (FR-033).
    /// </summary>
    NotEligible,

    /// <summary>One or more obligations are outstanding; nothing was changed (FR-011, FR-038).</summary>
    Blocked,

    /// <summary>
    /// The erasure failed and was rolled back; the account is intact (FR-042). The member is told it
    /// did not happen, with no internal detail.
    /// </summary>
    Failed,
}

/// <summary>
/// The result of an erasure attempt. Carries blockers only when <see cref="Outcome"/> is
/// <see cref="AccountDeletionOutcome.Blocked"/>.
/// </summary>
public sealed record AccountDeletionResult(
    AccountDeletionOutcome Outcome,
    IReadOnlyList<DeletionBlockerDto> Blockers)
{
    public static AccountDeletionResult Done() => new(AccountDeletionOutcome.Done, []);

    public static AccountDeletionResult Fail(AccountDeletionOutcome outcome) => new(outcome, []);

    public static AccountDeletionResult Blocked(IReadOnlyList<DeletionBlockerDto> blockers) =>
        new(AccountDeletionOutcome.Blocked, blockers);
}

/// <summary>
/// Self-service account erasure (feature 037, GDPR Art. 17). The member erases their own account;
/// there is no admin-initiated variant here, because administrators already have ban — a different
/// remedy with deliberately different semantics (feature 013).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here takes an account id.</b> Every operation is scoped to the caller, so no request
/// shape exists in which one member could target another (spec FR-002, Principle I).
/// </para>
/// <para>
/// <b>What "erasure" means here.</b> The <c>User</c> row survives — roughly twenty foreign keys into
/// it are <c>DeleteBehavior.Restrict</c> by design, each protecting a record that must outlive the
/// account (moderation history, other people's awards, conversations other people are also in). So
/// the profile is deleted and the account row is neutralised: after this runs, the surviving row
/// identifies nobody.
/// </para>
/// </remarks>
public interface IAccountDeletionService
{
    /// <summary>
    /// What would happen, and whether it may happen. Read-only; every answer is re-checked inside
    /// the transaction at confirmation time (FR-013).
    /// </summary>
    Task<AccountDeletionPreviewDto?> PreviewAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Erase the caller's account. All-or-nothing (FR-038): either the account is gone or it is
    /// exactly as it was.
    /// </summary>
    Task<AccountDeletionResult> DeleteAsync(
        Guid userId,
        string password,
        string confirmation,
        CancellationToken ct = default);
}
