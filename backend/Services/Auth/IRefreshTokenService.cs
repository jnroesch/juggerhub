namespace JuggerHub.Services.Auth;

/// <summary>
/// Issues, rotates, and revokes refresh tokens. The raw token is returned to the
/// caller (to set as a cookie) but only its hash is persisted. Rotation is
/// single-use; presenting an already-rotated/expired token is treated as reuse and
/// revokes the entire family. See specs/002-authentication/research.md §2.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Issue a new refresh token. Pass an existing <paramref name="familyId"/> to
    /// continue a rotation chain, or null to start a new family (fresh login).</summary>
    Task<IssuedRefreshToken> IssueAsync(Guid userId, bool isPersistent, string? ip, Guid? familyId = null, CancellationToken ct = default);

    /// <summary>Validate and rotate the presented raw token (single-use). On reuse of a
    /// rotated/expired token, revokes the whole family.</summary>
    Task<RotateResult> RotateAsync(string rawToken, string? ip, CancellationToken ct = default);

    /// <summary>Revoke the single token matching the raw value (e.g. logout). No-op if not found.</summary>
    Task RevokeAsync(string rawToken, string reason, CancellationToken ct = default);

    /// <summary>Revoke all active tokens for a user (e.g. on password reset/change).</summary>
    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default);
}

/// <summary>A freshly issued refresh token: the raw value (cookie) plus metadata.</summary>
public readonly record struct IssuedRefreshToken(string RawToken, Guid TokenId, DateTime ExpiresAt, bool IsPersistent);

public enum RotateStatus
{
    Success,
    Invalid,
    ReuseDetected,
}

/// <summary>Outcome of a rotation attempt.</summary>
public sealed class RotateResult
{
    public RotateStatus Status { get; init; }
    public Guid UserId { get; init; }
    public IssuedRefreshToken? Issued { get; init; }

    public static RotateResult Invalid() => new() { Status = RotateStatus.Invalid };
    public static RotateResult Reuse() => new() { Status = RotateStatus.ReuseDetected };
    public static RotateResult Success(Guid userId, IssuedRefreshToken issued) =>
        new() { Status = RotateStatus.Success, UserId = userId, Issued = issued };
}
