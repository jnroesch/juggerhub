namespace JuggerHub.Common;

/// <summary>
/// The configured platform-administrator identities (feature 013, closing GitHub issue #21).
/// Since 013 this is no longer a live per-request gate: at startup
/// <c>PlatformAdminRoleSync</c> MIRRORS the <c>PlatformAdmin</c> Identity role to this list
/// (additions grant, removals revoke, not-yet-registered emails are picked up at a later
/// startup). Authorization checks the role, never this config directly. Changing the list
/// requires a restart to take effect; an empty list means zero admins (fail closed).
/// </summary>
/// <remarks>
/// Bound from the <c>Admin</c> config section. Locally the value flows through <c>.env</c>
/// (<c>ADMIN_EMAILS</c> → <c>Admin__Emails</c>); deployed environments set it in the GitHub
/// Environment. A single comma-separated string is used (rather than a JSON array) so one env
/// var carries the whole list. This is NOT a secret, but it is environment-specific config.
/// </remarks>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>Comma-separated platform-admin emails, e.g. <c>admin@test.de,ops@test.de</c>.</summary>
    public string Emails { get; set; } = string.Empty;

    /// <summary>
    /// Normalized (trimmed, lower-cased) admin emails for the startup role sync.
    /// Empty when unconfigured — the role is then emptied and every admin operation
    /// is refused (fails closed).
    /// </summary>
    public IReadOnlyList<string> NormalizedEmails => Emails
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(e => e.ToLowerInvariant())
        .ToArray();
}
