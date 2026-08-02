namespace JuggerHub.Entities;

/// <summary>
/// Platform-level account state (feature 013). Exactly one per account; transitions
/// happen only through the admin user service, which records each change as an
/// <see cref="AdminActionRecord"/>.
/// </summary>
/// <remarks>
/// Semantics (spec 013, owner-clarified):
/// <list type="bullet">
/// <item><b>Active</b> — normal account.</item>
/// <item><b>Suspended</b> — sign-in and refresh are refused; everything else (profile,
/// visibility, memberships, content) is untouched.</item>
/// <item><b>Banned</b> — soft-deleted: invisible on all player-facing surfaces (global
/// query filter on <see cref="PlayerProfile"/>), sign-in refused, and the retained row's
/// unique email doubles as the re-registration denylist. Fully reversible.</item>
/// <item><b>Deleted</b> — genuinely erased at the member's own request (feature 037).
/// <b>Terminal and irreversible</b>, unlike the three above.</item>
/// </list>
/// </remarks>
public enum AccountStatus
{
    Active = 0,
    Suspended = 1,
    Banned = 2,

    /// <summary>
    /// The member erased their own account (feature 037, GDPR Art. 17). The
    /// <see cref="User"/> row survives — ~20 foreign keys into it are
    /// <c>DeleteBehavior.Restrict</c> by design, each protecting a record that must outlive
    /// the account — but every identifying column on it has been neutralised and the
    /// <see cref="PlayerProfile"/> is gone. The surviving row identifies nobody.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Terminal.</b> There is no transition out of this state: no reinstate, no unban, no
    /// restore path (spec FR-029). <c>AdminUserService</c> must never list it in a
    /// transition's <c>from:</c> set. This is the one <see cref="AccountStatus"/> value that
    /// is not reversible, and the distinction from <see cref="Banned"/> is the whole point —
    /// a ban is a *retained* soft-delete, this is an *erasure*.
    /// </para>
    /// <para>
    /// <b>Re-registration differs from <see cref="Banned"/> on purpose</b> (spec FR-032). A
    /// banned row keeps its email, so <c>UserManager.FindByEmailAsync</c> still finds it and
    /// registration is refused — that retained address *is* the denylist. A deleted row has
    /// released its email (and its username, which registration also derives from the
    /// address), so the same lookup finds nothing and the member may register again.
    /// </para>
    /// <para>
    /// <b>⚠ Adding a value here is not free.</b> Predicates written as
    /// <c>Status != AccountStatus.Banned</c> silently admit every new value. When this one was
    /// added, three such predicates in <c>ChatConversationService</c> queried
    /// <c>Users</c> directly and would have treated an erased account as contactable; they
    /// were rewritten as positive tests (<c>== Active || == Suspended</c>). The four global
    /// query filters were safe only incidentally, because the row they filter on cascades
    /// away with the profile. Audit both groups before adding a fifth value.
    /// </para>
    /// </remarks>
    Deleted = 3,
}

/// <summary>
/// The administrative account actions recorded in <see cref="AdminActionRecord"/>
/// (append-only; feature 013 FR-017).
/// </summary>
public enum AdminAccountAction
{
    Suspend = 0,
    Reinstate = 1,
    Ban = 2,
    Unban = 3,
    PasswordResetSent = 4,
}
