using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Parties;

/// <summary>A caller's resolved access to a party (feature 016).</summary>
public readonly record struct PartyAccess(
    Guid PartyId,
    Guid TeamId,
    Guid EventId,
    PartyStatus Status,
    EventStatus EventStatus,
    DateTime EventEndsAt,
    PartyMemberRole? MyRole,
    PartyMemberStatus? MyStatus,
    bool IsTeamMember)
{
    /// <summary>The caller is a party admin (creator or accepted co-admin).</summary>
    public bool IsPartyAdmin => MyRole == PartyMemberRole.Admin;

    /// <summary>The caller is part of the crew (an <see cref="PartyMemberStatus.In"/> member).</summary>
    public bool IsCrew => MyStatus == PartyMemberStatus.In;

    /// <summary>The event still accepts activity (not cancelled, not ended).</summary>
    public bool IsEventOpen => EventStatus != EventStatus.Cancelled && EventEndsAt >= DateTime.UtcNow;
}

/// <summary>
/// Resolves a caller's role/state for a party id in a single query. Every party service uses it so
/// authorization is uniform and enforced server-side (constitution Principle I). A caller who is not
/// a member of the party's team is treated as if the party were unknown (member-gated reads map a
/// null result — or <see cref="PartyAccess.IsTeamMember"/> == false — to 404), mirroring teams.
/// </summary>
public sealed class PartyGuard
{
    private readonly AppDbContext _db;

    public PartyGuard(AppDbContext db) => _db = db;

    /// <summary>
    /// Resolve the party's team/event/status plus the caller's role/status and team membership.
    /// Returns null when no party has that id.
    /// </summary>
    public async Task<PartyAccess?> ResolveAsync(Guid partyId, Guid userId, CancellationToken ct = default)
    {
        var match = await _db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId)
            .Select(p => new
            {
                p.TeamId,
                p.EventId,
                p.Status,
                EventStatus = p.Event.Status,
                p.Event.EndsAt,
                MyRole = p.Members
                    .Where(m => m.UserId == userId)
                    .Select(m => (PartyMemberRole?)m.Role)
                    .FirstOrDefault(),
                MyStatus = p.Members
                    .Where(m => m.UserId == userId)
                    .Select(m => (PartyMemberStatus?)m.Status)
                    .FirstOrDefault(),
                IsTeamMember = p.Team.Memberships.Any(tm => tm.UserId == userId),
            })
            .FirstOrDefaultAsync(ct);

        return match is null
            ? null
            : new PartyAccess(partyId, match.TeamId, match.EventId, match.Status, match.EventStatus,
                match.EndsAt, match.MyRole, match.MyStatus, match.IsTeamMember);
    }
}
