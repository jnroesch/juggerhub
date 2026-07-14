using JuggerHub.Common;
using JuggerHub.Dtos.Parties;

namespace JuggerHub.Services.Parties;

/// <summary>
/// Private party news feed (feature 016): list (crew-only) and create (party-admin, notifies the
/// crew in-app + email, mirroring team news).
/// </summary>
public interface IPartyNewsService
{
    /// <summary>The party news feed (crew-only); null when the caller is not in the crew.</summary>
    Task<PagedResult<PartyNewsDto>?> ListAsync(Guid partyId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Post a party news update (party-admin); notifies the crew.</summary>
    Task<PartyResult<PartyNewsDto>> CreateAsync(Guid partyId, string body, Guid actorUserId, CancellationToken ct = default);
}
