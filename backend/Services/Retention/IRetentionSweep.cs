namespace JuggerHub.Services.Retention;

/// <summary>
/// One category of data that is deleted on a schedule (GH #106).
/// </summary>
/// <remarks>
/// The platform had no automated deletion of any kind until this existed, which is what made
/// the privacy policy's retention section hard to write honestly. #106 names further candidates
/// (notifications, admin action records, archived chat snapshots); each becomes another
/// implementation registered alongside this one, and <see cref="RetentionBackgroundService"/>
/// runs whatever it finds.
/// <para>
/// A sweep must be safe to run concurrently with itself. The API runs more than one replica and
/// every replica hosts the background service, so several will overlap by design — deleting by
/// age keeps that harmless, since the second run simply matches nothing.
/// </para>
/// </remarks>
public interface IRetentionSweep
{
    /// <summary>Short identifier for logs, e.g. <c>expired-refresh-tokens</c>.</summary>
    string Name { get; }

    /// <summary>Deletes everything past its retention period. Returns the number of rows removed.</summary>
    Task<int> SweepAsync(CancellationToken ct = default);
}
