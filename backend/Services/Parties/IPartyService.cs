using JuggerHub.Common;
using JuggerHub.Dtos.Parties;

namespace JuggerHub.Services.Parties;

/// <summary>
/// Party lifecycle + discovery (feature 016): form, read detail/context, list a team's requests,
/// apply to the event, withdraw, and disband. Authorization is enforced server-side (Principle I).
/// </summary>
public interface IPartyService
{
    /// <summary>Form a party for a teams-only event; the caller must administer the team.</summary>
    Task<PartyResult<PartyDto>> FormAsync(Guid eventId, Guid teamId, string? message, Guid actorUserId, CancellationToken ct = default);

    /// <summary>The caller's party affordances for an event (teams they admin + existing parties).</summary>
    Task<PartyContextDto?> GetContextAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Party detail (member-gated); null when the caller is not a member of the party's team.</summary>
    Task<PartyDto?> GetDetailAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>The pinned party-request cards a team member can see; null when not a team member.</summary>
    Task<PagedResult<PartyRequestCardDto>?> ListTeamRequestsAsync(string slug, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Apply the party to the event as the team's entry (feature-006 flow); party-admin only.</summary>
    Task<PartyResult<PartyDto>> ApplyAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Withdraw the team's event entry, keeping the party; party-admin only.</summary>
    Task<PartyResult<PartyDto>> WithdrawAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Disband (hard-delete) the party, withdrawing any applied entry; party-admin only.</summary>
    Task<PartyResult> DisbandAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);
}
