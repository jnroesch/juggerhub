using JuggerHub.Common;
using JuggerHub.Dtos.Parties;

namespace JuggerHub.Services.Parties;

/// <summary>
/// Team-scoped party co-admin invitations (feature 016), mirroring the event invitation slice:
/// link + targeted invites, member search, preview, accept, decline, revoke.
/// </summary>
public interface IPartyInvitationService
{
    Task<PartyResult<PartyInviteLinkDto?>> GetActiveLinkAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);

    Task<PartyResult<PartyInviteLinkDto>> CreateOrRotateLinkAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);

    Task<PartyResult> RevokeAsync(Guid partyId, Guid actorUserId, Guid invitationId, CancellationToken ct = default);

    Task<PartyResult<PartyInvitationDto>> CreateTargetedAsync(Guid partyId, Guid actorUserId, Guid targetUserId, CancellationToken ct = default);

    Task<PartyResult<PagedResult<PartyInvitationDto>>> ListPendingAsync(Guid partyId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    Task<PartyResult<PagedResult<PartyInvitableUserDto>>> SearchMembersAsync(Guid partyId, Guid actorUserId, string query, PaginationRequest pagination, CancellationToken ct = default);

    Task<PartyInvitePreviewDto?> GetPreviewAsync(string token, CancellationToken ct = default);

    /// <summary>Accept a co-admin invite; returns the party id on success.</summary>
    Task<PartyResult<Guid>> AcceptAsync(string token, Guid userId, CancellationToken ct = default);

    Task<PartyResult> DeclineAsync(string token, Guid userId, CancellationToken ct = default);
}
