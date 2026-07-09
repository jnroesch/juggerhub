namespace JuggerHub.Common;

/// <summary>
/// The <b>temporary</b> platform-administrator gate for feature 012 (Badges &amp; Achievements).
/// No platform system-admin role exists yet, so admin capabilities are gated by a configured
/// allowlist of admin emails, enforced server-side by the <c>PlatformAdmin</c> authorization
/// policy. This is an interim measure — a real admin role is tracked in GitHub issue #21, which
/// will replace only the policy handler, leaving controllers and behaviour untouched.
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
    /// Normalized (trimmed, lower-cased) admin emails for case-insensitive membership checks.
    /// Empty when unconfigured — the policy then denies everyone (fails closed).
    /// </summary>
    public IReadOnlyList<string> NormalizedEmails => Emails
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(e => e.ToLowerInvariant())
        .ToArray();
}
