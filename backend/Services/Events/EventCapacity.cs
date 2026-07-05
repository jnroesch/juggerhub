using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Events;

/// <summary>
/// Capacity accounting for events. Occupied spots = <see cref="SignupStatus.Joined"/> +
/// <see cref="SignupStatus.AwaitingApproval"/>; a <see cref="SignupStatus.Waitlisted"/> entry
/// never counts. Sign-up / approve / promote paths take a pessimistic lock on the event row
/// (<c>SELECT … FOR UPDATE</c>) before counting + writing, so concurrent last-spot operations
/// can never exceed the limit — mirroring the team last-admin guard (research §4).
/// </summary>
public sealed class EventCapacity
{
    private readonly AppDbContext _db;

    public EventCapacity(AppDbContext db) => _db = db;

    /// <summary>Count occupied spots (Joined + AwaitingApproval) for an event.</summary>
    public Task<int> OccupiedCountAsync(Guid eventId, CancellationToken ct = default) =>
        _db.EventSignups.CountAsync(
            s => s.EventId == eventId
                && (s.Status == SignupStatus.Joined || s.Status == SignupStatus.AwaitingApproval),
            ct);

    /// <summary>
    /// Pessimistically lock the event row for the current transaction, so a capacity check and
    /// the subsequent write are atomic. Callers must already be inside a transaction.
    /// </summary>
    public Task LockEventRowAsync(Guid eventId, CancellationToken ct = default) =>
        _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Events\" WHERE \"Id\" = {eventId} FOR UPDATE", ct);
}
