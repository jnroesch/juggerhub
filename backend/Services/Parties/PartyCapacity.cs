using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Parties;

/// <summary>
/// Roster-cap accounting for a party (feature 016). Occupied roster spots =
/// <see cref="PartyMemberStatus.In"/> members; declined/no-response never count. The join path takes
/// a pessimistic lock on the party row (<c>SELECT … FOR UPDATE</c>) before counting + writing, so
/// concurrent last-spot joins can never exceed the cap — mirroring <c>EventCapacity</c>. There is no
/// party-level waiting list: at the cap joining auto-closes, and a drop auto-reopens it first-come.
/// </summary>
public sealed class PartyCapacity
{
    private readonly AppDbContext _db;

    public PartyCapacity(AppDbContext db) => _db = db;

    /// <summary>Count crew (In) members for a party.</summary>
    public Task<int> InCountAsync(Guid partyId, CancellationToken ct = default) =>
        _db.PartyMembers.CountAsync(m => m.PartyId == partyId && m.Status == PartyMemberStatus.In, ct);

    /// <summary>
    /// Pessimistically lock the party row for the current transaction, so a cap check and the
    /// subsequent write are atomic. Callers must already be inside a transaction.
    /// </summary>
    public Task LockPartyRowAsync(Guid partyId, CancellationToken ct = default) =>
        _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Parties\" WHERE \"Id\" = {partyId} FOR UPDATE", ct);
}
