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

    /// <summary>
    /// How long after a rotation the superseded token is answered as a LOST RACE rather than as theft
    /// (GH #247). The client single-flights refresh per tab, not across tabs: two tabs waking from
    /// sleep send the same cookie at once, and the one that loses must not be treated as a replay —
    /// that would revoke the family and sign the member out of every tab. Seconds cover that; anything
    /// presented later is reuse, exactly as before.
    /// </summary>
    internal static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(30);

    private const string RotatedReason = "rotated";

    public async Task<IssuedRefreshToken> IssueAsync(
        Guid userId, bool isPersistent, string? ip, Guid? familyId = null, CancellationToken ct = default)
    {
        var (entity, raw) = NewToken(userId, isPersistent, ip, familyId ?? Guid.NewGuid());
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new IssuedRefreshToken(raw, entity.Id, entity.ExpiresAt, isPersistent);
    }

    /// <remarks>
    /// One refresh token has exactly ONE successor, enforced by the database rather than by a
    /// check-then-act (GH #247): the token is claimed with a conditional UPDATE, and only the request
    /// that claims it issues a replacement. Before this, two concurrent refreshes could both pass the
    /// "not revoked yet" check and both mint a live successor — and a thief racing the legitimate
    /// client that way was never detected as reuse.
    /// <para>
    /// A request that presents a token rotated within <see cref="RotationGrace"/> — the loser of such
    /// a race — gets <see cref="RotateStatus.AlreadyRotated"/>: the caller issues an access token
    /// only, never a refresh token, and the family is left alone. So a lost race costs nothing, a
    /// thief gains at most one short-lived access token inside the window, and a replay after it is
    /// reuse and revokes the family as always.
    /// </para>
    /// </remarks>
    public async Task<RotateResult> RotateAsync(string rawToken, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return RotateResult.Invalid();
        }

        var hash = Hash(rawToken);
        var token = await _db.RefreshTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash)
            .Select(t => new { t.Id, t.UserId, t.FamilyId, t.IsPersistent, t.ExpiresAt, t.RevokedAt, t.RevokedReason, t.ReplacedByTokenId })
            .FirstOrDefaultAsync(ct);
        if (token is null)
        {
            return RotateResult.Invalid();
        }

        if (token.RevokedAt is not null)
        {
            if (IsLostRace(token.RevokedReason, token.RevokedAt, token.ReplacedByTokenId))
            {
                return RotateResult.AlreadyRotated(token.UserId, token.IsPersistent);
            }

            // Presented again outside the grace window, or revoked for any other reason: theft/replay.
            // Revoking the whole family is the safe response.
            await RevokeFamilyAsync(token.FamilyId, "reuse-detected", ct);
            return RotateResult.Reuse();
        }

        if (token.ExpiresAt <= DateTime.UtcNow)
        {
            await RevokeFamilyAsync(token.FamilyId, "reuse-detected", ct);
            return RotateResult.Reuse();
        }

        var issued = await ClaimAndReplaceAsync(token.Id, token.UserId, token.IsPersistent, token.FamilyId, ip, ct);
        if (issued is not null)
        {
            return RotateResult.Success(token.UserId, issued.Value);
        }

        // Lost the claim: another request changed this row between our read and our UPDATE. That
        // UPDATE waited for the winner's row lock, so the outcome is committed and visible now.
        var current = await _db.RefreshTokens.AsNoTracking()
            .Where(t => t.Id == token.Id)
            .Select(t => new { t.RevokedReason, t.RevokedAt, t.ReplacedByTokenId })
            .FirstAsync(ct);
        return IsLostRace(current.RevokedReason, current.RevokedAt, current.ReplacedByTokenId)
            ? RotateResult.AlreadyRotated(token.UserId, token.IsPersistent)
            // Revoked for another reason in the meantime (sign-out, password reset): nothing to grant.
            : RotateResult.Invalid();
    }

    /// <summary>
    /// Claim <paramref name="tokenId"/> and insert its successor as ONE transaction, run through the
    /// execution strategy as a single retriable unit (constitution Principle VII). Returns null when
    /// the claim matched no row — someone else rotated or revoked it first.
    /// </summary>
    /// <remarks>
    /// Replay-safe by construction: a retried attempt re-runs the conditional claim, so it can never
    /// produce a second successor. If a commit succeeded but its acknowledgement was lost, the retry
    /// finds the row already rotated and returns null — the member gets an access token now and signs
    /// in again later, never two refresh tokens.
    /// </remarks>
    private async Task<IssuedRefreshToken?> ClaimAndReplaceAsync(
        Guid tokenId, Guid userId, bool isPersistent, Guid familyId, string? ip, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        RefreshToken? pending = null;

        return await strategy.ExecuteAsync(async () =>
        {
            // A rolled-back attempt leaves its row in the change tracker (028), and the next
            // SaveChanges would insert it alongside the new one. Detach it first.
            if (pending is not null)
            {
                _db.Entry(pending).State = EntityState.Detached;
                pending = null;
            }

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var now = DateTime.UtcNow;

            // ExecuteUpdate bypasses the audit interceptor, so ModifiedDate is set explicitly (Principle III).
            var claimed = await _db.RefreshTokens
                .Where(t => t.Id == tokenId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedReason, RotatedReason)
                    .SetProperty(t => t.ModifiedDate, now), ct);
            if (claimed == 0)
            {
                await tx.RollbackAsync(ct);
                return (IssuedRefreshToken?)null;
            }

            var (successor, raw) = NewToken(userId, isPersistent, ip, familyId);
            pending = successor;
            _db.RefreshTokens.Add(successor);
            await _db.SaveChangesAsync(ct);

            await _db.RefreshTokens
                .Where(t => t.Id == tokenId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.ReplacedByTokenId, successor.Id)
                    .SetProperty(t => t.ModifiedDate, now), ct);

            await tx.CommitAsync(ct);
            return new IssuedRefreshToken(raw, successor.Id, successor.ExpiresAt, isPersistent);
        });
    }

    /// <summary>Rotated (not revoked for any other reason), with a committed successor, recently.</summary>
    private static bool IsLostRace(string? revokedReason, DateTime? revokedAt, Guid? replacedBy) =>
        revokedReason == RotatedReason
        && replacedBy is not null
        && revokedAt is not null
        && revokedAt.Value > DateTime.UtcNow - RotationGrace;

    private static (RefreshToken Entity, string Raw) NewToken(Guid userId, bool isPersistent, string? ip, Guid familyId)
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            FamilyId = familyId,
            ExpiresAt = DateTime.UtcNow.Add(isPersistent ? PersistentLifetime : SessionLifetime),
            IsPersistent = isPersistent,
            CreatedByIp = ip,
        };
        return (entity, raw);
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
