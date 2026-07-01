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

    public User User { get; set; } = null!;

    public ICollection<ProfilePompfe> Pompfen { get; set; } = [];

    public ProfileAvatar? Avatar { get; set; }

    public ICollection<EventParticipation> Participations { get; set; } = [];
}
