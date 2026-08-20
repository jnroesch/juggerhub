using JuggerHub.Common;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Media;

/// <summary>Outcome of a reconciliation sweep.</summary>
/// <param name="Examined">Objects considered (those past the grace period).</param>
/// <param name="Reclaimed">Objects deleted because no descriptor referenced them.</param>
/// <param name="Skipped">Objects left alone because they were newer than the grace period.</param>
public readonly record struct MediaReconciliationResult(int Examined, int Reclaimed, int Skipped);

/// <summary>
/// Reclaims stored objects that no descriptor row references (feature 035 / #97, FR-030).
/// </summary>
/// <remarks>
/// <para>
/// Orphans arise in two ways. A failure between writing an object and committing its descriptor
/// leaves one behind — rare, and the deliberately harmless side of an ordering that cannot be
/// transactional, because a database row and a blob cannot share a transaction. The other way is a
/// database-level cascade delete, which removes the descriptor inside PostgreSQL with no
/// application code running and therefore no chance to delete the object. That second path is why
/// this sweep is a <b>correctness guarantee rather than housekeeping</b>: for cascades it is the
/// only thing that ever reclaims the bytes.
/// </para>
/// <para>
/// <b>Operator-triggered, not scheduled</b> (spec Clarifications). Orphans are rare — nothing
/// hard-deletes a media owner today, since bans are soft-delete — and inert, since their keys
/// existed only in the deleted rows and the container is private. Against that, an unattended
/// process whose job is deleting media is a hazard if its grace-period logic is wrong. Keeping a
/// human in the loop is worth more than a tighter bound on a failure mode that currently cannot
/// occur. Revisit when a hard-delete or right-to-erasure path is added.
/// </para>
/// </remarks>
public sealed class MediaReconciliationService
{
    private readonly AppDbContext _db;
    private readonly IMediaStore _store;
    private readonly MediaStorageOptions _options;
    private readonly ILogger<MediaReconciliationService> _logger;

    public MediaReconciliationService(
        AppDbContext db,
        IMediaStore store,
        IOptions<MediaStorageOptions> options,
        ILogger<MediaReconciliationService> logger)
    {
        _db = db;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Delete every stored object no descriptor references and old enough to be safe.</summary>
    public async Task<MediaReconciliationResult> SweepAsync(CancellationToken ct = default)
    {
        // Referenced keys are read up front, across every descriptor table. IgnoreQueryFilters
        // is essential: the ProfileAvatars ban filter would otherwise hide a banned member's row,
        // the sweep would see their object as unreferenced, and it would delete media belonging to
        // an account that is suspended rather than gone — irreversibly, and exactly the wrong call.
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in await _db.ProfileAvatars.IgnoreQueryFilters().Select(a => a.ObjectKey).ToListAsync(ct))
        {
            referenced.Add(key);
        }

        foreach (var key in await _db.BadgeIcons.IgnoreQueryFilters().Select(i => i.ObjectKey).ToListAsync(ct))
        {
            referenced.Add(key);
        }

        foreach (var key in await _db.AchievementIcons.IgnoreQueryFilters().Select(i => i.ObjectKey).ToListAsync(ct))
        {
            referenced.Add(key);
        }

        // Feature 046 (#99) — the two showcase galleries. These loops are not optional bookkeeping:
        // this method deletes every object it does not find here, so a gallery table missing from
        // this list means the next sweep destroys every showcase image in the environment. The
        // ProfileShowcaseImages read needs IgnoreQueryFilters for the same reason ProfileAvatars
        // does — its ban filter would otherwise hide a suspended member's live objects and the sweep
        // would reclaim them. If a future feature adds another descriptor table, it belongs here too.
        foreach (var key in await _db.ProfileShowcaseImages.IgnoreQueryFilters().Select(g => g.ObjectKey).ToListAsync(ct))
        {
            referenced.Add(key);
        }

        foreach (var key in await _db.TeamShowcaseImages.IgnoreQueryFilters().Select(g => g.ObjectKey).ToListAsync(ct))
        {
            referenced.Add(key);
        }

        // Anything written within the grace period is left alone. An object is stored BEFORE its
        // descriptor row commits, so there is always a brief, legitimate window in which a live
        // upload looks unreferenced — without this, the sweep would race uploads and destroy them.
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-_options.OrphanGraceMinutes);

        var examined = 0;
        var reclaimed = 0;
        var skipped = 0;

        // Streamed, so memory stays flat regardless of how many objects the container holds.
        await foreach (var stored in _store.ListAsync(prefix: null, ct))
        {
            if (referenced.Contains(stored.Key))
            {
                continue;
            }

            if (stored.LastModified > cutoff)
            {
                skipped++;
                continue;
            }

            examined++;
            await _store.DeleteAsync(stored.Key, ct);
            reclaimed++;
        }

        // Key names are logged: they are internal identifiers, not credentials or media content,
        // and an operator needs them to reason about what a sweep removed. Counts only here.
        _logger.LogInformation(
            "Media reconciliation reclaimed {Reclaimed} unreferenced object(s); {Skipped} left inside the {GraceMinutes}-minute grace period.",
            reclaimed,
            skipped,
            _options.OrphanGraceMinutes);

        return new MediaReconciliationResult(examined, reclaimed, skipped);
    }
}
