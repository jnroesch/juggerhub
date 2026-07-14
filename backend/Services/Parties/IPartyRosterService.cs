using JuggerHub.Common;
using JuggerHub.Dtos.Parties;

namespace JuggerHub.Services.Parties;

/// <summary>
/// Party roster operations (feature 016): list groups (In/Declined/NoResponse), join, decline,
/// leave, remove, and nudge. Capacity is enforced atomically under a party-row lock.
/// </summary>
public interface IPartyRosterService
{
    /// <summary>A roster group (member-gated); null when the caller is not a member of the party's team.</summary>
    Task<PagedResult<PartyMemberDto>?> ListGroupAsync(Guid partyId, PartyRosterGroup group, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>"I'm in" — join the crew if a spot is open (team member, self).</summary>
    Task<PartyResult<PartyMemberDto>> JoinAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>"Can't make it" — decline (reversible; team member, self).</summary>
    Task<PartyResult<PartyMemberDto>> DeclineAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Leave the crew (self); the last admin cannot leave.</summary>
    Task<PartyResult> LeaveAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Remove a member from the party (party-admin); never touches team membership.</summary>
    Task<PartyResult> RemoveAsync(Guid partyId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Re-send the request to a member who hasn't answered (party-admin).</summary>
    Task<PartyResult> NudgeAsync(Guid partyId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default);
}
