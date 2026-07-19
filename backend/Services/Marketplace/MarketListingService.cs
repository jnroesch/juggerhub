using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Marketplace;
using JuggerHub.Entities;
using JuggerHub.Services.Parties;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Marketplace;

/// <summary>
/// EF-Core-direct implementation of <see cref="IMarketListingService"/> (feature 017). Posting is
/// gated on eligibility (<see cref="MarketEligibility"/>) and a teams, still-open event; the unique
/// (user, event) index backstops the one-listing rule. Take-down (and joining a party — see
/// <c>MarketRequestService</c>) hard-delete the row.
/// </summary>
public sealed class MarketListingService : IMarketListingService
{
    private readonly AppDbContext _db;
    private readonly MarketEligibility _eligibility;

    public MarketListingService(AppDbContext db, MarketEligibility eligibility)
    {
        _db = db;
        _eligibility = eligibility;
    }

    public async Task<PagedResult<MarketListingCardDto>> ListFreeAgentsAsync(
        Guid eventId, Pompfe? position, PaginationRequest pagination, CancellationToken ct = default)
    {
        var query = _db.MercenaryListings.AsNoTracking().Where(l => l.EventId == eventId);
        if (position is Pompfe p)
        {
            query = query.Where(l => l.Positions.Contains(p));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(l => new MarketListingCardDto(
                l.UserId,
                l.User.Profile!.Handle,
                l.User.Profile!.DisplayName,
                l.User.Profile!.Avatar != null,
                l.Positions,
                l.Pitch))
            .ToListAsync(ct);

        return new PagedResult<MarketListingCardDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<PagedResult<MyListingDto>> ListMyListingsAsync(
        Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // "Currently active" = listings on events that are still open (not cancelled, not ended).
        var query = _db.MercenaryListings.AsNoTracking()
            .Where(l => l.UserId == actorUserId
                && l.Event.Status != EventStatus.Cancelled && l.Event.EndsAt >= now);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(l => l.Event.StartsAt)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(l => new MyListingDto(l.Id, l.EventId, l.Event.Name, l.Event.StartsAt, l.Positions, l.Pitch))
            .ToListAsync(ct);

        return new PagedResult<MyListingDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<PartyResult<MarketListingDto>> PostAsync(
        Guid eventId, Guid actorUserId, PostListingRequest request, CancellationToken ct = default)
    {
        var gate = await GateEventAsync(eventId, ct);
        if (gate is not null)
        {
            return PartyResult<MarketListingDto>.Fail(gate.Value.Outcome, gate.Value.Error);
        }

        var positions = Normalize(request.Positions);
        if (positions.Count == 0)
        {
            return PartyResult<MarketListingDto>.Fail(PartyOutcome.Invalid, "Pick at least one position you'd play.");
        }

        var pitch = (request.Pitch ?? string.Empty).Trim();
        if (pitch.Length is 0 or > 280)
        {
            return PartyResult<MarketListingDto>.Fail(PartyOutcome.Invalid, "Write a short pitch (up to 280 characters).");
        }

        if (await _eligibility.IsInAPartyAsync(eventId, actorUserId, ct))
        {
            return PartyResult<MarketListingDto>.Fail(PartyOutcome.Conflict, "You're already in a crew for this event — one event, one crew.");
        }

        if (await _db.MercenaryListings.AnyAsync(l => l.EventId == eventId && l.UserId == actorUserId, ct))
        {
            return PartyResult<MarketListingDto>.Fail(PartyOutcome.Conflict, "You already have a listing here — edit it instead.");
        }

        var listing = new MercenaryListing
        {
            EventId = eventId,
            UserId = actorUserId,
            Positions = positions,
            Pitch = pitch,
        };
        _db.MercenaryListings.Add(listing);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (MarketErrors.IsUniqueViolation(ex))
        {
            return PartyResult<MarketListingDto>.Fail(PartyOutcome.Conflict, "You already have a listing here — edit it instead.");
        }

        return PartyResult<MarketListingDto>.Ok(new MarketListingDto(listing.Id, eventId, listing.Positions, listing.Pitch));
    }

    public async Task<PartyResult<MarketListingDto>> EditAsync(
        Guid eventId, Guid actorUserId, PostListingRequest request, CancellationToken ct = default)
    {
        var gate = await GateEventAsync(eventId, ct);
        if (gate is not null)
        {
            return PartyResult<MarketListingDto>.Fail(gate.Value.Outcome, gate.Value.Error);
        }

        var positions = Normalize(request.Positions);
        if (positions.Count == 0)
        {
            return PartyResult<MarketListingDto>.Fail(PartyOutcome.Invalid, "Pick at least one position you'd play.");
        }

        var pitch = (request.Pitch ?? string.Empty).Trim();
        if (pitch.Length is 0 or > 280)
        {
            return PartyResult<MarketListingDto>.Fail(PartyOutcome.Invalid, "Write a short pitch (up to 280 characters).");
        }

        var listing = await _db.MercenaryListings.FirstOrDefaultAsync(l => l.EventId == eventId && l.UserId == actorUserId, ct);
        if (listing is null)
        {
            return PartyResult<MarketListingDto>.Fail(PartyOutcome.NotFound, "You don't have a listing here yet.");
        }

        listing.Positions = positions;
        listing.Pitch = pitch;
        await _db.SaveChangesAsync(ct);

        return PartyResult<MarketListingDto>.Ok(new MarketListingDto(listing.Id, eventId, listing.Positions, listing.Pitch));
    }

    public async Task<PartyResult> TakeDownAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default)
    {
        await _db.MercenaryListings
            .Where(l => l.EventId == eventId && l.UserId == actorUserId)
            .ExecuteDeleteAsync(ct);
        return PartyResult.Ok();
    }

    // --- Helpers --------------------------------------------------------------

    /// <summary>Validate the event exists, is a teams event, and is still open. Null = ok.</summary>
    private async Task<(PartyOutcome Outcome, string? Error)?> GateEventAsync(Guid eventId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.ParticipantMode, e.Status, e.EndsAt })
            .FirstOrDefaultAsync(ct);
        if (ev is null)
        {
            return (PartyOutcome.NotFound, "No such event.");
        }

        if (ev.ParticipantMode != ParticipantMode.Teams)
        {
            return (PartyOutcome.Invalid, "The marketplace is only for teams events.");
        }

        if (ev.Status == EventStatus.Cancelled || ev.EndsAt < DateTime.UtcNow)
        {
            return (PartyOutcome.Closed, "This event isn't accepting the marketplace.");
        }

        return null;
    }

    internal static List<Pompfe> Normalize(IReadOnlyList<Pompfe>? positions) =>
        positions is null ? [] : positions.Distinct().ToList();
}
