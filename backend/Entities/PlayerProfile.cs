namespace JuggerHub.Entities;

/// <summary>
/// The public-facing player identity for an account (1:1 with <see cref="User"/>).
/// Profile data lives here — a <see cref="BaseEntity"/> — rather than on the
/// Identity user, keeping Identity clean (see specs/003-profile/data-model.md).
/// </summary>
/// <remarks>
/// The <see cref="Handle"/> is unique and IMMUTABLE: it addresses the public
/// profile (<c>/u/&lt;handle&gt;</c>) and is set once at registration, with no
/// service or endpoint that mutates it. Enforced by a unique index plus the
/// private init-only setter.
/// </remarks>
public sealed class PlayerProfile : BaseEntity
{
    /// <summary>Owning account (FK → AspNetUsers). Unique (1:1).</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Unique, URL-safe, immutable handle (<c>@handle</c>, <c>/u/&lt;handle&gt;</c>).
    /// Set once at creation; no update path exists.
    /// </summary>
    public string Handle { get; init; } = string.Empty;

    /// <summary>Shown name; defaults to the handle at creation so the page is never blank.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional short free-text hometown.</summary>
    public string? Hometown { get; set; }

    /// <summary>Optional short bio (≤ 280 chars).</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Owner-controlled anonymous visibility (feature 026). <c>false</c> (private, the default)
    /// = an anonymous caller gets the same 404 as a missing handle; <c>true</c> (public) = the
    /// profile is viewable anonymously at <c>/u/&lt;handle&gt;</c>. Authenticated callers can view
    /// any profile regardless. Independent of ban state — the global query filter still hides a
    /// banned owner even when this is <c>true</c>. Changed only by the owner via their own update.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// When the owner finished (or dismissed) first-login onboarding, in UTC.
    /// <c>null</c> = not yet onboarded (the guided flow still shows on sign-in);
    /// a value = onboarded. Set once and idempotently (see specs/004-onboarding);
    /// there is no reset path. Exposed to the owner as the <c>OnboardingCompleted</c>
    /// boolean on <c>AuthUserDto</c>, never as an editable profile field.
    /// </summary>
    public DateTime? OnboardingCompletedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<ProfilePompfe> Pompfen { get; set; } = [];

    public ProfileAvatar? Avatar { get; set; }

    public ICollection<EventParticipation> Participations { get; set; } = [];
}
