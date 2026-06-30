namespace JuggerHub.Entities;

/// <summary>
/// One issued refresh token. The raw token value is never stored — only its
/// <see cref="TokenHash"/> (SHA-256) — so a database read cannot yield a usable
/// session credential. Rotation creates a new row in the same <see cref="FamilyId"/>
/// and revokes the old; presenting a revoked/expired token triggers reuse detection
/// (the whole family is revoked). See specs/002-authentication/research.md §2.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    /// <summary>Owning user (FK → AspNetUsers).</summary>
    public Guid UserId { get; set; }

    /// <summary>Base64 SHA-256 hash of the raw token. Unique. The raw value is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Shared across one login's rotation chain; reuse detection revokes by family.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>Successor token id, set when this token is rotated. Null while current.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    /// <summary>Absolute expiry (UTC).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Remember-me: persistent cookie vs session-only.</summary>
    public bool IsPersistent { get; set; }

    /// <summary>Set on rotation, logout, password reset, or reuse detection. Null = active.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Originating IP (audit aid). Never a token/secret.</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>Why it was revoked (e.g. rotated, logout, password-reset, reuse-detected).</summary>
    public string? RevokedReason { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Active = not revoked and not expired.</summary>
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
