using JuggerHub.Common;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Retention;

/// <summary>
/// Deletes refresh token rows whose absolute expiry passed more than
/// <see cref="RetentionOptions.RefreshTokenGraceDays"/> ago (GH #106).
/// </summary>
/// <remarks>
/// <para>
/// Refresh tokens are the highest-value category on that issue's list for one reason: they are
/// the only place on the platform that stores an IP address (<c>RefreshToken.CreatedByIp</c>,
/// disclosed in the privacy policy), and they accumulate a row per sign-in and per rotation
/// forever. Nothing here is member-authored content — deleting a spent session record takes
/// nothing away from anyone.
/// </para>
/// <para>
/// <b>The cutoff is <see cref="Entities.RefreshToken.ExpiresAt"/>, never
/// <see cref="Entities.RefreshToken.RevokedAt"/>, and that is a security decision rather than a
/// stylistic one.</b> <c>RefreshTokenService.RotateAsync</c> treats a presented row that is
/// revoked *or* expired as evidence of theft and revokes the entire token family. Rotation
/// revokes a row within minutes of issuing it, so sweeping on <c>RevokedAt</c> would delete
/// almost every row almost immediately — and a replayed token would then find no row at all,
/// downgrading the response from "tear down the family" to "reject this request". Access is
/// refused either way, but a family kept alive by continued rotation would survive. Keying on
/// expiry plus a grace period preserves that response for the whole window in which a stolen
/// token is plausibly still worth replaying.
/// </para>
/// <para>
/// <c>ExecuteDeleteAsync</c> issues one <c>DELETE ... WHERE</c> and loads nothing into memory,
/// so the sweep costs the same whether it removes one row or a hundred thousand. It also bypasses
/// the audit interceptor, which is correct here — the rows are going away, not changing.
/// </para>
/// </remarks>
public sealed class ExpiredRefreshTokenSweep : IRetentionSweep
{
    private readonly AppDbContext _db;
    private readonly RetentionOptions _options;

    public ExpiredRefreshTokenSweep(AppDbContext db, IOptions<RetentionOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public string Name => "expired-refresh-tokens";

    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.RefreshTokenGraceDays);

        return await _db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
