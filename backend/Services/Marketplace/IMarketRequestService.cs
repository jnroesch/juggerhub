using JuggerHub.Common;
using JuggerHub.Dtos.Marketplace;
using JuggerHub.Services.Parties;

namespace JuggerHub.Services.Marketplace;

/// <summary>
/// The two-way handshake of the marketplace (feature 017): applications (user → party) and invites
/// (party → user, board or direct), their accept/decline/revoke transitions, both inboxes, the
/// caller's per-event market context, the cross-event dashboard summary, and the direct-invite user
/// search. Accepting seats a guest atomically under the party-row lock and cleans up the joiner's other
/// pending requests + listing. Every action is authorized server-side by direction/role.
/// </summary>
public interface IMarketRequestService
{
    /// <summary>The signed-in caller's marketplace context for one event (eligibility, listing, admin parties, invites to answer, applications). Null when the event is unknown.</summary>
    Task<MyMarketDto?> GetMyMarketAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>A free agent applies to a recruiting party.</summary>
    Task<PartyResult<MarketRequestDto>> ApplyAsync(Guid partyId, Guid actorUserId, ApplyRequest request, CancellationToken ct = default);

    /// <summary>A party admin invites a user (board card or direct search); delivers a notification + email.</summary>
    Task<PartyResult<MarketRequestDto>> InviteAsync(Guid partyId, Guid actorUserId, InviteRequest request, CancellationToken ct = default);

    /// <summary>The recipient accepts — seats the user as a party member (guest if not on the team), atomically.</summary>
    Task<PartyResult<MarketRequestDto>> AcceptAsync(Guid requestId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>The recipient declines (drops the request).</summary>
    Task<PartyResult<MarketRequestDto>> DeclineAsync(Guid requestId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>The initiator revokes/withdraws a pending request.</summary>
    Task<PartyResult> RevokeAsync(Guid requestId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>A party's pending applications (party admin).</summary>
    Task<PartyResult<PagedResult<MarketRequestDto>>> ListPartyApplicationsAsync(Guid partyId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>A party's sent invites — awaiting + declined (party admin).</summary>
    Task<PartyResult<PagedResult<MarketRequestDto>>> ListPartyInvitesAsync(Guid partyId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The caller's cross-event market items for the dashboard (pending invites + applications).</summary>
    Task<PagedResult<MyMarketRequestDto>> ListMineAsync(Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Search any user by name/@handle for a direct invite from this party (party admin).</summary>
    Task<PartyResult<PagedResult<MarketInvitableUserDto>>> SearchInvitableAsync(Guid partyId, Guid actorUserId, string query, PaginationRequest pagination, CancellationToken ct = default);
}
