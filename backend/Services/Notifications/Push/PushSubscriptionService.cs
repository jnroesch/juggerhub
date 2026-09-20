using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuggerHub.Services.Notifications.Push;

/// <summary>EF-Core-direct implementation of <see cref="IPushSubscriptionService"/> (feature 055).</summary>
public sealed class PushSubscriptionService : IPushSubscriptionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PushSubscriptionService> _logger;

    public PushSubscriptionService(AppDbContext db, ILogger<PushSubscriptionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RegisterAsync(
        Guid userId,
        string endpoint,
        string p256dh,
        string auth,
        string? deviceLabel,
        CancellationToken ct = default)
        => RegisterAsync(userId, endpoint, p256dh, auth, deviceLabel, allowRetry: true, ct);

    private async Task RegisterAsync(
        Guid userId,
        string endpoint,
        string p256dh,
        string auth,
        string? deviceLabel,
        bool allowRetry,
        CancellationToken ct)
    {
        // Tracked, not projected: the row is about to be written, and the audit-field interceptor
        // only runs for tracked saves (constitution III).
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

        if (existing is not null)
        {
            // Already ours: refresh the keys, which a browser may rotate without changing the
            // endpoint, and the label, which changes when the browser updates.
            //
            // Someone else's: MOVE it. A push endpoint identifies a browser, not a person, so this
            // is the shared-device case — one phone, two people, one after the other. Reassigning
            // is what stops the previous owner receiving the new owner's notifications. The unique
            // index on Endpoint is what makes this a move rather than a duplicate.
            if (existing.UserId != userId)
            {
                _logger.LogInformation(
                    "Push endpoint reassigned to a different account (subscription {SubscriptionId}).",
                    existing.Id);
                existing.UserId = userId;
                // A reassigned device has never successfully received anything FOR THIS account.
                existing.LastSuccessAt = null;
            }

            existing.P256dh = p256dh;
            existing.Auth = auth;
            existing.DeviceLabel = deviceLabel;
            await _db.SaveChangesAsync(ct);
            return;
        }

        _db.PushSubscriptions.Add(new PushSubscription
        {
            UserId = userId,
            Endpoint = endpoint,
            P256dh = p256dh,
            Auth = auth,
            DeviceLabel = deviceLabel,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex) && allowRetry)
        {
            // Two tabs of the same browser registering at once, racing on the unique endpoint
            // index. The row now exists, so take the update path instead. Clear the tracker first:
            // the failed insert is still staged in it and would be retried by the next save.
            //
            // allowRetry bounds this to exactly one more attempt. A second unique violation is not
            // a race any more and is allowed to surface.
            _db.ChangeTracker.Clear();
            await RegisterAsync(userId, endpoint, p256dh, auth, deviceLabel, allowRetry: false, ct);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid userId, string endpoint, CancellationToken ct = default)
    {
        // Scoped to the caller, so an endpoint belonging to someone else matches nothing. The
        // result is not reported: whether a row existed is not the caller's business, and saying
        // so would turn this into an oracle for which endpoints are registered.
        await _db.PushSubscriptions
            .Where(s => s.UserId == userId && s.Endpoint == endpoint)
            .ExecuteDeleteAsync(ct);
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
