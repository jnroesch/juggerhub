using Microsoft.AspNetCore.Identity;

namespace JuggerHub.Entities;

/// <summary>
/// The platform member / identity-foundation entity.
/// </summary>
/// <remarks>
/// Derives from <see cref="IdentityUser{TKey}"/> with a <see cref="Guid"/> key
/// (UUIDv7, aligned with <see cref="BaseEntity"/>). This scaffold adds no custom
/// profile fields — display name, roles, and membership semantics arrive with
/// later features. Sign-up / sign-in / password-reset flows are intentionally
/// deferred; only the identity foundation and its persistence exist here.
/// </remarks>
public class User : IdentityUser<Guid>
{
    /// <summary>
    /// The player's profile (1:1). Created atomically with the account at
    /// registration (feature 003); see <see cref="Entities.PlayerProfile"/>.
    /// </summary>
    public PlayerProfile? Profile { get; set; }

    /// <summary>
    /// Platform account state (feature 013). Enforced in login/refresh and — for
    /// <see cref="AccountStatus.Banned"/> — via the global query filter that hides
    /// the profile from every player-facing surface. Changed only by the admin
    /// user service, which records each transition.
    /// </summary>
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    /// <summary>UTC of the last <see cref="Status"/> transition; null if never changed.</summary>
    public DateTime? StatusChangedAt { get; set; }

    /// <summary>
    /// The user's chosen interface language (feature 031). A supported BCP-47 base tag
    /// (<c>"en"</c>/<c>"de"</c>/<c>"es"</c>), or <c>null</c> when the user has not chosen one —
    /// in which case the client resolves a language by detection (browser) and the backend
    /// localizes their emails/notifications in English. Set via <c>PUT /account/language</c> and
    /// validated against the supported allowlist server-side (never trust the client).
    /// </summary>
    public string? PreferredLanguage { get; set; }
}
