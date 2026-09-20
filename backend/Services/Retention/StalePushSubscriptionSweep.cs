using JuggerHub.Common;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Retention;

/// <summary>
/// Deletes push subscriptions that have been unreachable for longer than
/// <see cref="RetentionOptions.PushSubscriptionIdleDays"/> (feature 055).
/// </summary>
/// <remarks>
/// <para>
/// This is the <em>second</em> line of defence, not the first. A dead subscription is normally
/// removed the moment a push service answers 404 or 410, which happens on the next delivery
/// attempt. The gap this closes is the subscription nothing is ever sent to — because its owner's
/// categories are all off, or because nothing concerning them has happened — and which therefore
/// never produces such an answer. Without the sweep those rows accumulate for ever, and they carry
/// a per-device delivery address the privacy policy says has a limit.
/// </para>
/// <para>
/// The cutoff uses the last successful delivery, falling back to when the row was created for a
/// subscription that has never received anything. Both columns are indexed for exactly this query.
/// </para>
/// <para>
/// <c>ExecuteDeleteAsync</c> issues one <c>DELETE ... WHERE</c> and loads nothing into memory. It
/// bypasses the audit interceptor, which is correct here — the rows are going away, not changing.
/// </para>
/// </remarks>
public sealed class StalePushSubscriptionSweep : IRetentionSweep
{
    private readonly AppDbContext _db;
    private readonly RetentionOptions _options;

    public StalePushSubscriptionSweep(AppDbContext db, IOptions<RetentionOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public string Name => "stale-push-subscriptions";

    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.PushSubscriptionIdleDays);

        return await _db.PushSubscriptions
            .Where(s => (s.LastSuccessAt ?? s.CreatedDate) < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
