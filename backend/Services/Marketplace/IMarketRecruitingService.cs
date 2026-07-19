using JuggerHub.Common;
using JuggerHub.Dtos.Marketplace;
using JuggerHub.Entities;
using JuggerHub.Services.Parties;

namespace JuggerHub.Services.Marketplace;

/// <summary>
/// A party's opt-in recruiting on the marketplace board (feature 017): the party-admin toggle/settings
/// and the public parties-recruiting board side. Off by default; only a party admin may change it.
/// </summary>
public interface IMarketRecruitingService
{
    /// <summary>The board's parties-recruiting side for an event (recruiting parties with an open spot). Public.</summary>
    Task<PagedResult<RecruitingPartyCardDto>> ListRecruitingPartiesAsync(
        Guid eventId, Pompfe? position, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The party's recruiting settings + live fill (party admin only).</summary>
    Task<PartyResult<RecruitingSettingsDto>> GetAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Toggle/set the party's recruiting settings (party admin only; refused on a closed event).</summary>
    Task<PartyResult<RecruitingSettingsDto>> SetAsync(
        Guid partyId, Guid actorUserId, SetRecruitingRequest request, CancellationToken ct = default);
}
