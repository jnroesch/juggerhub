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
/// </list>
/// </remarks>
public enum AccountStatus
{
    Active = 0,
    Suspended = 1,
    Banned = 2,
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
