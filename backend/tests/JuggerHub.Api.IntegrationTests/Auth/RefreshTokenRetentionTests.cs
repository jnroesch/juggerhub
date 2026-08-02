using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>
/// GH #106 — automated deletion of spent refresh tokens.
/// </summary>
/// <remarks>
/// The privacy policy states a retention period for sign-in records, so these are checks on a
/// published legal claim rather than on housekeeping. Two of them pull in opposite directions and
/// both matter: rows past the period must actually go, and rows inside it must actually stay,
/// because the grace period is what keeps reuse detection working
/// (<c>RefreshTokenService.RotateAsync</c> revokes a whole family when a revoked or expired row is
/// presented, and a deleted row cannot trigger that).
/// </remarks>
[Collection("Auth")]
public sealed class RefreshTokenRetentionTests
{
    private readonly JuggerHubApiFactory _factory;

    public RefreshTokenRetentionTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Sweep_deletes_tokens_expired_beyond_the_grace_period()
    {
        var userId = await NewUserAsync();
        var grace = GraceDays();

        var wayPastGrace = await SeedTokenAsync(userId, expiresAt: Ago(grace + 10));
        var justPastGrace = await SeedTokenAsync(userId, expiresAt: Ago(grace + 1));

        await SweepAsync();

        Assert.False(await ExistsAsync(wayPastGrace));
        Assert.False(await ExistsAsync(justPastGrace));
    }

    [Fact]
    public async Task Sweep_keeps_expired_tokens_that_are_still_inside_the_grace_period()
    {
        // The reuse-detection guarantee. An attacker replaying a token stolen last week must still
        // hit a row, so that the family it belongs to — which rotation may have kept alive — is torn
        // down rather than the single request merely being refused.
        var userId = await NewUserAsync();

        var expiredYesterday = await SeedTokenAsync(userId, expiresAt: Ago(1), revoked: true);
        var expiredJustInsideGrace = await SeedTokenAsync(userId, expiresAt: Ago(GraceDays() - 1));

        await SweepAsync();

        Assert.True(await ExistsAsync(expiredYesterday));
        Assert.True(await ExistsAsync(expiredJustInsideGrace));
    }

    [Fact]
    public async Task Sweep_keeps_active_tokens()
    {
        var userId = await NewUserAsync();
        var active = await SeedTokenAsync(userId, expiresAt: DateTime.UtcNow.AddDays(7));

        await SweepAsync();

        Assert.True(await ExistsAsync(active));
    }

    [Fact]
    public async Task Sweep_ignores_revocation_and_keys_only_on_expiry()
    {
        // Rotation revokes a token within minutes of issuing it. Sweeping on RevokedAt would
        // therefore delete nearly every row almost immediately and gut reuse detection, so a
        // freshly revoked but unexpired token has to survive.
        var userId = await NewUserAsync();
        var revokedButCurrent = await SeedTokenAsync(
            userId, expiresAt: DateTime.UtcNow.AddDays(13), revoked: true);

        await SweepAsync();

        Assert.True(await ExistsAsync(revokedButCurrent));
    }

    [Fact]
    public async Task Sweep_leaves_other_users_tokens_alone()
    {
        var (mine, theirs) = (await NewUserAsync(), await NewUserAsync());
        var expired = await SeedTokenAsync(mine, expiresAt: Ago(GraceDays() + 5));
        var theirActive = await SeedTokenAsync(theirs, expiresAt: DateTime.UtcNow.AddDays(1));

        var deleted = await SweepAsync();

        Assert.False(await ExistsAsync(expired));
        Assert.True(await ExistsAsync(theirActive));
        Assert.True(deleted >= 1);
    }

    [Fact]
    public void Retention_sweeps_run_from_a_hosted_service()
    {
        // The factory disables the timer, so nothing else here would notice if the background
        // service were dropped from Program.cs and the sweep silently stopped running in Prod.
        var hosted = _factory.Services.GetServices<IHostedService>();

        Assert.Contains(hosted, s => s is RetentionBackgroundService);
    }

    // --- helpers ------------------------------------------------------------

    private int GraceDays() =>
        _factory.Services.GetRequiredService<IOptions<RetentionOptions>>().Value.RefreshTokenGraceDays;

    private static DateTime Ago(int days) => DateTime.UtcNow.AddDays(-days);

    private async Task<Guid> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var (userId, _) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);
        return userId;
    }

    private async Task<Guid> SeedTokenAsync(Guid userId, DateTime expiresAt, bool revoked = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            FamilyId = Guid.NewGuid(),
            ExpiresAt = expiresAt,
            CreatedByIp = "203.0.113.7",
            RevokedAt = revoked ? expiresAt.AddMinutes(-1) : null,
            RevokedReason = revoked ? "rotated" : null,
        };

        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();
        return token.Id;
    }

    private async Task<int> SweepAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sweep = scope.ServiceProvider.GetRequiredService<IRetentionSweep>();
        return await sweep.SweepAsync();
    }

    private async Task<bool> ExistsAsync(Guid tokenId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.RefreshTokens.AnyAsync(t => t.Id == tokenId);
    }
}
