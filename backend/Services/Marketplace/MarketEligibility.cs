using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Marketplace;

/// <summary>
/// The single "one event, one crew" invariant for the marketplace (feature 017): a user is eligible to
/// post a listing, apply, or be invited <em>iff</em> they do not already hold a crew seat at the event
/// — i.e. they are not <see cref="PartyMemberStatus.In"/> any party for that event. Every post/apply/
/// invite/accept path checks this server-side; accept re-checks it atomically under the party-row lock.
/// </summary>
public sealed class MarketEligibility
{
    private readonly AppDbContext _db;

    public MarketEligibility(AppDbContext db) => _db = db;

    /// <summary>True if <paramref name="userId"/> is already In a party for <paramref name="eventId"/>.</summary>
    public Task<bool> IsInAPartyAsync(Guid eventId, Guid userId, CancellationToken ct = default) =>
        _db.PartyMembers.AnyAsync(
            m => m.UserId == userId && m.Status == PartyMemberStatus.In && m.Party.EventId == eventId, ct);
}
