using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Marketplace;
using JuggerHub.Entities;
using JuggerHub.Services.Parties;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Marketplace;

/// <summary>
/// EF-Core-direct implementation of <see cref="IMarketRecruitingService"/> (feature 017). Recruiting is
/// stored on the <see cref="Party"/> (off by default) and toggled only by a party admin via
/// <see cref="PartyGuard"/>. The board shows recruiting parties that still have an open spot
/// (In-count &lt; roster cap, guests included), auto-hiding a full party and reopening it when a spot
/// frees — mirroring the feature-016 party request.
/// </summary>
public sealed class MarketRecruitingService : IMarketRecruitingService
{
    private readonly AppDbContext _db;
    private readonly PartyGuard _guard;

    public MarketRecruitingService(AppDbContext db, PartyGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    public async Task<PagedResult<RecruitingPartyCardDto>> ListRecruitingPartiesAsync(
        Guid eventId, Pompfe? position, PaginationRequest pagination, CancellationToken ct = default)
    {
        var query = _db.Parties.AsNoTracking()
            .Where(p => p.EventId == eventId && p.IsRecruiting
                && p.Members.Count(m => m.Status == PartyMemberStatus.In) < p.RosterCap);
        if (position is Pompfe pos)
        {
            query = query.Where(p => p.PositionsNeeded.Contains(pos));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.ModifiedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new
            {
                p.Id,
                p.TeamId,
                TeamName = p.Team.Name,
                TeamSlug = p.Team.Slug,
                p.EventId,
                p.RosterCap,
                InCount = p.Members.Count(m => m.Status == PartyMemberStatus.In),
                p.PositionsNeeded,
                p.RecruitBlurb,
            })
            .ToListAsync(ct);

        var cards = items.Select(p => new RecruitingPartyCardDto(
            p.Id, p.TeamId, p.TeamName, p.TeamSlug, p.EventId,
            Math.Max(0, p.RosterCap - p.InCount), p.RosterCap, p.InCount,
            p.PositionsNeeded, p.RecruitBlurb)).ToList();

        return new PagedResult<RecruitingPartyCardDto>(cards, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<PartyResult<RecruitingSettingsDto>> GetAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<RecruitingSettingsDto>.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult<RecruitingSettingsDto>.Fail(PartyOutcome.Forbidden, "Only a party admin can manage recruiting.");
        }

        return PartyResult<RecruitingSettingsDto>.Ok(await ProjectSettingsAsync(partyId, ct));
    }

    public async Task<PartyResult<RecruitingSettingsDto>> SetAsync(
        Guid partyId, Guid actorUserId, SetRecruitingRequest request, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<RecruitingSettingsDto>.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult<RecruitingSettingsDto>.Fail(PartyOutcome.Forbidden, "Only a party admin can manage recruiting.");
        }

        if (!access.Value.IsEventOpen)
        {
            return PartyResult<RecruitingSettingsDto>.Fail(PartyOutcome.Closed, "This event isn't accepting the marketplace.");
        }

        var party = await _db.Parties.FirstAsync(p => p.Id == partyId, ct);
        var blurb = string.IsNullOrWhiteSpace(request.Blurb) ? null : request.Blurb.Trim();
        if (blurb is { Length: > 500 })
        {
            return PartyResult<RecruitingSettingsDto>.Fail(PartyOutcome.Invalid, "Keep the blurb under 500 characters.");
        }

        party.IsRecruiting = request.IsRecruiting;
        party.SpotsAdvertised = Math.Clamp(request.SpotsAdvertised, 0, party.RosterCap);
        party.PositionsNeeded = MarketListingService.Normalize(request.PositionsNeeded);
        party.RecruitBlurb = blurb;
        await _db.SaveChangesAsync(ct);

        return PartyResult<RecruitingSettingsDto>.Ok(await ProjectSettingsAsync(partyId, ct));
    }

    private Task<RecruitingSettingsDto> ProjectSettingsAsync(Guid partyId, CancellationToken ct) =>
        _db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId)
            .Select(p => new RecruitingSettingsDto(
                p.Id,
                p.IsRecruiting,
                p.SpotsAdvertised,
                p.PositionsNeeded,
                p.RecruitBlurb,
                p.RosterCap,
                p.Members.Count(m => m.Status == PartyMemberStatus.In),
                Math.Max(0, p.RosterCap - p.Members.Count(m => m.Status == PartyMemberStatus.In))))
            .FirstAsync(ct);
}
