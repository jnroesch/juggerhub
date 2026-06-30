using System.Security.Cryptography;
using System.Text;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Auth;

/// <summary>
/// EF Core-backed refresh-token store. The token is 256 bits of full-entropy
/// randomness, so it is hashed with SHA-256 (not argon2 — there is nothing to
/// brute-force; a fast hash that prevents "DB read → usable token" is correct).
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    // Sliding per-token lifetime; persistent (remember-me) lasts longer than a session token.
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(1);
    private static readonly TimeSpan PersistentLifetime = TimeSpan.FromDays(14);
    private const int TokenBytes = 32; // 256-bit

    private readonly AppDbContext _db;

    public RefreshTokenService(AppDbContext db) => _db = db;

    public async Task<IssuedRefreshToken> IssueAsync(
        Guid userId, bool isPersistent, string? ip, Guid? familyId = null, CancellationToken ct = default)
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        var expiresAt = DateTime.UtcNow.Add(isPersistent ? PersistentLifetime : SessionLifetime);

        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            FamilyId = familyId ?? Guid.NewGuid(),
            ExpiresAt = expiresAt,
            IsPersistent = isPersistent,
            CreatedByIp = ip,
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new IssuedRefreshToken(raw, entity.Id, expiresAt, isPersistent);
    }

    public async Task<RotateResult> RotateAsync(string rawToken, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return RotateResult.Invalid();
        }

        var hash = Hash(rawToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
        {
            return RotateResult.Invalid();
        }

        if (token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow)
        {
            // A revoked (already-rotated) token presented again indicates theft/replay.
            // Revoking the whole family on reuse (or a stale expired token) is the safe response.
            await RevokeFamilyAsync(token.FamilyId, "reuse-detected", ct);
            return RotateResult.Reuse();
        }

        var issued = await IssueAsync(token.UserId, token.IsPersistent, ip, token.FamilyId, ct);

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedReason = "rotated";
        token.ReplacedByTokenId = issued.TokenId;
        await _db.SaveChangesAsync(ct);

        return RotateResult.Success(token.UserId, issued);
    }

    public async Task RevokeAsync(string rawToken, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return;
        }

        var hash = Hash(rawToken);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct);
        if (token is null)
        {
            return;
        }

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedReason = reason;
        await _db.SaveChangesAsync(ct);
    }

    public Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default) =>
        RevokeWhereAsync(t => t.UserId == userId && t.RevokedAt == null, reason, ct);

    private Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct) =>
        RevokeWhereAsync(t => t.FamilyId == familyId && t.RevokedAt == null, reason, ct);

    private async Task RevokeWhereAsync(
        System.Linq.Expressions.Expression<Func<RefreshToken, bool>> predicate, string reason, CancellationToken ct)
    {
        // ExecuteUpdateAsync bypasses the audit interceptor, so set ModifiedDate explicitly.
        var now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(predicate)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, reason)
                .SetProperty(t => t.ModifiedDate, now), ct);
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
