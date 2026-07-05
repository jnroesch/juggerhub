using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Events;

/// <summary>A caller's resolved access to an event.</summary>
public readonly record struct EventAccess(Guid EventId, EventStatus Status, bool IsAdmin)
{
    public bool IsCancelled => Status == EventStatus.Cancelled;
}

/// <summary>
/// Resolves a caller's admin status for an event id in a single query. Every event service uses
/// it so authorization is uniform and enforced server-side (constitution Principle I). Unlike
/// teams, the event page itself is public — this guard gates only admin actions; a non-admin
/// yields a resolved event with <see cref="EventAccess.IsAdmin"/> = false.
/// </summary>
public sealed class EventAdminGuard
{
    private readonly AppDbContext _db;

    public EventAdminGuard(AppDbContext db) => _db = db;

    /// <summary>
    /// Resolve the event's status + whether the caller is an admin. Returns null when no event
    /// has that id. A caller with no <see cref="EventAdmin"/> row yields a non-null result whose
    /// <see cref="EventAccess.IsAdmin"/> is false.
    /// </summary>
    public async Task<EventAccess?> ResolveAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var match = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new
            {
                e.Status,
                IsAdmin = e.Admins.Any(a => a.UserId == userId),
            })
            .FirstOrDefaultAsync(ct);

        return match is null ? null : new EventAccess(eventId, match.Status, match.IsAdmin);
    }
}
