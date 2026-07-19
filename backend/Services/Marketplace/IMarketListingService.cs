using JuggerHub.Common;
using JuggerHub.Dtos.Marketplace;
using JuggerHub.Entities;
using JuggerHub.Services.Parties;

namespace JuggerHub.Services.Marketplace;

/// <summary>
/// Free-agent listings on a teams event's marketplace board (feature 017): the caller's own
/// post/edit/take-down and the public free-agents board side. Eligibility ("not In a party here") and
/// the one-listing-per-(user,event) rule are enforced server-side.
/// </summary>
public interface IMarketListingService
{
    /// <summary>The board's free-agents side for an event, optionally filtered to one position. Public.</summary>
    Task<PagedResult<MarketListingCardDto>> ListFreeAgentsAsync(
        Guid eventId, Pompfe? position, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The caller's active free-agent listings across all still-open events (dashboard).</summary>
    Task<PagedResult<MyListingDto>> ListMyListingsAsync(
        Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Post the caller's free-agent listing (must be eligible and have no live listing yet).</summary>
    Task<PartyResult<MarketListingDto>> PostAsync(
        Guid eventId, Guid actorUserId, PostListingRequest request, CancellationToken ct = default);

    /// <summary>Edit the caller's live listing.</summary>
    Task<PartyResult<MarketListingDto>> EditAsync(
        Guid eventId, Guid actorUserId, PostListingRequest request, CancellationToken ct = default);

    /// <summary>Take the caller's listing down (idempotent).</summary>
    Task<PartyResult> TakeDownAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default);
}
