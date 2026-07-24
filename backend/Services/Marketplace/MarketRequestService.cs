using System.Linq.Expressions;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Marketplace;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Notifications;
using JuggerHub.Services.Parties;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Marketplace;

/// <summary>
/// EF-Core-direct implementation of <see cref="IMarketRequestService"/> (feature 017). Creating a
/// request is gated on eligibility and an open spot; the filtered-unique (party, user) pending index
/// backstops the "≤ 1 active per pair" rule. Accepting reuses the feature-016 pessimistic party-row
/// lock so the cap can never be exceeded, seats the user as a guest (unless they are on the team),
/// revokes their other pending requests, and takes down their listing ("one event, one crew"). Invites
/// notify the target (in-app + email); applications surface only in the party's recruiting inbox.
/// </summary>
public sealed class MarketRequestService : IMarketRequestService
{
    private readonly AppDbContext _db;
    private readonly PartyGuard _guard;
    private readonly PartyCapacity _capacity;
    private readonly MarketEligibility _eligibility;
    private readonly INotificationService _notifications;
    private readonly MarketEmailService _email;

    public MarketRequestService(
        AppDbContext db, PartyGuard guard, PartyCapacity capacity, MarketEligibility eligibility,
        INotificationService notifications, MarketEmailService email)
    {
        _db = db;
        _guard = guard;
        _capacity = capacity;
        _eligibility = eligibility;
        _notifications = notifications;
        _email = email;
    }

    /// <summary>Reusable projection of a request to its inbox DTO (party + user identities).</summary>
    private static readonly Expression<Func<MarketRequest, MarketRequestDto>> ToDto = r => new MarketRequestDto(
        r.Id, r.PartyId, r.Party.Team.Name, r.Party.Team.Slug, r.Party.EventId, r.Party.Event.Name,
        r.UserId, r.User.Profile!.Handle, r.User.Profile!.DisplayName, r.User.Profile!.Avatar != null,
        r.Direction, r.Positions, r.Status, r.CreatedDate);

    // --- Event context (/me) --------------------------------------------------

    public async Task<MyMarketDto?> GetMyMarketAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default)
    {
        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.ParticipantMode, e.Status, e.EndsAt })
            .FirstOrDefaultAsync(ct);
        if (ev is null)
        {
            return null;
        }

        if (ev.ParticipantMode != ParticipantMode.Teams)
        {
            return new MyMarketDto(actorUserId, ev.ParticipantMode, false, null, null, [], [], []);
        }

        var eventOpen = ev.Status != EventStatus.Cancelled && ev.EndsAt >= DateTime.UtcNow;
        var inAParty = await _eligibility.IsInAPartyAsync(eventId, actorUserId, ct);
        var eligible = eventOpen && !inAParty;
        var reason = inAParty ? "You're already in a crew for this event."
            : !eventOpen ? "This event isn't accepting the marketplace."
            : null;

        var myListing = await _db.MercenaryListings.AsNoTracking()
            .Where(l => l.EventId == eventId && l.UserId == actorUserId)
            .Select(l => new MarketListingDto(l.Id, l.EventId, l.Positions, l.Pitch))
            .FirstOrDefaultAsync(ct);

        var adminParties = await _db.Parties.AsNoTracking()
            .Where(p => p.EventId == eventId && p.Members.Any(m => m.UserId == actorUserId && m.Role == PartyMemberRole.Admin))
            .Select(p => new MyMarketAdminPartyDto(
                p.Id, p.Team.Name, p.Team.Slug, p.IsRecruiting,
                Math.Max(0, p.RosterCap - p.Members.Count(m => m.Status == PartyMemberStatus.In))))
            .ToListAsync(ct);

        var invitesToAnswer = await _db.MarketRequests.AsNoTracking()
            .Where(r => r.UserId == actorUserId && r.Direction == MarketRequestDirection.Invite
                && r.Status == MarketRequestStatus.Pending && r.Party.EventId == eventId)
            .OrderByDescending(r => r.CreatedDate).Take(50).Select(ToDto).ToListAsync(ct);

        var myApplications = await _db.MarketRequests.AsNoTracking()
            .Where(r => r.UserId == actorUserId && r.Direction == MarketRequestDirection.Application
                && (r.Status == MarketRequestStatus.Pending || r.Status == MarketRequestStatus.Declined)
                && r.Party.EventId == eventId)
            .OrderByDescending(r => r.CreatedDate).Take(50).Select(ToDto).ToListAsync(ct);

        return new MyMarketDto(actorUserId, ParticipantMode.Teams, eligible, reason, myListing, adminParties, invitesToAnswer, myApplications);
    }

    // --- Apply ----------------------------------------------------------------

    public async Task<PartyResult<MarketRequestDto>> ApplyAsync(Guid partyId, Guid actorUserId, ApplyRequest request, CancellationToken ct = default)
    {
        var party = await _db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId)
            .Select(p => new
            {
                p.EventId,
                p.IsRecruiting,
                p.RosterCap,
                EventStatus = p.Event.Status,
                p.Event.EndsAt,
                InCount = p.Members.Count(m => m.Status == PartyMemberStatus.In),
            })
            .FirstOrDefaultAsync(ct);
        if (party is null)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.NotFound, "No such party.");
        }

        if (party.EventStatus == EventStatus.Cancelled || party.EndsAt < DateTime.UtcNow)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Closed, "This event isn't accepting the marketplace.");
        }

        if (!party.IsRecruiting)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Conflict, "This party isn't looking for players right now.");
        }

        if (party.InCount >= party.RosterCap)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Full, "This party is full right now.");
        }

        if (await _eligibility.IsInAPartyAsync(party.EventId, actorUserId, ct))
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Conflict, "You're already in a crew for this event — one event, one crew.");
        }

        if (await HasActiveAsync(partyId, actorUserId, ct))
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Conflict, "You already have a pending request with this party.");
        }

        var req = new MarketRequest
        {
            PartyId = partyId,
            UserId = actorUserId,
            Direction = MarketRequestDirection.Application,
            Positions = MarketListingService.Normalize(request.Positions),
            Status = MarketRequestStatus.Pending,
            CreatedByUserId = actorUserId,
        };
        _db.MarketRequests.Add(req);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (MarketErrors.IsUniqueViolation(ex))
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Conflict, "You already have a pending request with this party.");
        }

        return PartyResult<MarketRequestDto>.Ok(await LoadDtoAsync(req.Id, ct));
    }

    // --- Invite ---------------------------------------------------------------

    public async Task<PartyResult<MarketRequestDto>> InviteAsync(Guid partyId, Guid actorUserId, InviteRequest request, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.NotFound, "No such party.");
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Forbidden, "Only a party admin can invite players.");
        }

        if (!access.Value.IsEventOpen)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Closed, "This event isn't accepting the marketplace.");
        }

        var eventId = access.Value.EventId;
        var cap = await _db.Parties.AsNoTracking().Where(p => p.Id == partyId)
            .Select(p => new { p.RosterCap, InCount = p.Members.Count(m => m.Status == PartyMemberStatus.In) })
            .FirstAsync(ct);
        if (cap.InCount >= cap.RosterCap)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Full, "Your party is full — free a spot before inviting.");
        }

        if (request.UserId == actorUserId)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Invalid, "You can't invite yourself.");
        }

        var target = await _db.Users.AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(u => new { u.Email, Name = u.Profile!.DisplayName })
            .FirstOrDefaultAsync(ct);
        if (target is null)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.NotFound, "No such player.");
        }

        if (await _eligibility.IsInAPartyAsync(eventId, request.UserId, ct))
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Conflict, "That player is already in a crew for this event.");
        }

        if (await HasActiveAsync(partyId, request.UserId, ct))
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Conflict, "There's already a pending request with that player.");
        }

        var req = new MarketRequest
        {
            PartyId = partyId,
            UserId = request.UserId,
            Direction = MarketRequestDirection.Invite,
            Positions = MarketListingService.Normalize(request.Positions),
            Status = MarketRequestStatus.Pending,
            CreatedByUserId = actorUserId,
        };
        _db.MarketRequests.Add(req);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (MarketErrors.IsUniqueViolation(ex))
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Conflict, "There's already a pending request with that player.");
        }

        await DeliverInviteAsync(req, partyId, eventId, actorUserId, request.UserId, target.Email, target.Name, ct);
        return PartyResult<MarketRequestDto>.Ok(await LoadDtoAsync(req.Id, ct));
    }

    /// <summary>Notify + email the invited player (never throws into the invite action; feature 010/011).</summary>
    private async Task DeliverInviteAsync(
        MarketRequest req, Guid partyId, Guid eventId, Guid actorUserId, Guid targetUserId,
        string? targetEmail, string targetName, CancellationToken ct)
    {
        var info = await _db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId)
            .Select(p => new { TeamName = p.Team.Name, TeamSlug = p.Team.Slug, EventName = p.Event.Name })
            .FirstAsync(ct);
        var inviterName = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == actorUserId).Select(p => p.DisplayName).FirstOrDefaultAsync(ct) ?? "A party admin";

        await _notifications.CreateAsync(
            targetUserId,
            NotificationType.MarketInvite,
            new { requestId = req.Id, partyId, info.TeamName, info.TeamSlug, eventId, info.EventName, positions = req.Positions },
            actorUserId,
            dedupeKey: $"market-invite:{req.Id}",
            ct);

        if (!string.IsNullOrEmpty(targetEmail))
        {
            await _email.SendMarketInviteEmailAsync(targetEmail, targetName, info.TeamName, info.EventName, inviterName, eventId, ct);
        }
    }

    // --- Accept ---------------------------------------------------------------

    public async Task<PartyResult<MarketRequestDto>> AcceptAsync(Guid requestId, Guid actorUserId, CancellationToken ct = default)
    {
        var meta = await _db.MarketRequests.AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new
            {
                r.PartyId,
                r.UserId,
                r.Direction,
                r.Status,
                r.Party.EventId,
                r.Party.TeamId,
                r.Party.RosterCap,
                EventStatus = r.Party.Event.Status,
                r.Party.Event.EndsAt,
            })
            .FirstOrDefaultAsync(ct);
        if (meta is null)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.NotFound, "No such request.");
        }

        // Recipient authorization by direction: an application is accepted by a party admin; an invite
        // by the targeted player.
        if (meta.Direction == MarketRequestDirection.Application)
        {
            var access = await _guard.ResolveAsync(meta.PartyId, actorUserId, ct);
            if (access is null || !access.Value.IsPartyAdmin)
            {
                return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Forbidden, "Only a party admin can accept applications.");
            }
        }
        else if (meta.UserId != actorUserId)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Forbidden, "Only the invited player can accept this invite.");
        }

        if (meta.EventStatus == EventStatus.Cancelled || meta.EndsAt < DateTime.UtcNow)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Closed, "This event isn't accepting the marketplace.");
        }

        // Connection resiliency (feature 028): lock, re-check eligibility and cap, seat the player
        // and revoke their other requests — all ONE retriable unit. Every check is inside the
        // delegate because a replay must re-validate against current state: the roster cap and the
        // "one event, one crew" rule are both enforced here and nowhere else.
        var strategy = _db.Database.CreateExecutionStrategy();
        var failure = await strategy.ExecuteAsync<PartyResult<MarketRequestDto>?>(async () =>
        {
            _db.ChangeTracker.Clear();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _capacity.LockPartyRowAsync(meta.PartyId, ct);

            var req = await _db.MarketRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
            if (req is null || req.Status != MarketRequestStatus.Pending)
            {
                return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Conflict, "This request is no longer pending.");
            }

            // Re-check eligibility atomically — the user may have joined another crew since.
            var alreadyIn = await _db.PartyMembers.AnyAsync(
                m => m.UserId == meta.UserId && m.Status == PartyMemberStatus.In && m.Party.EventId == meta.EventId, ct);
            if (alreadyIn)
            {
                return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Conflict, "That player is already in a crew for this event.");
            }

            var inCount = await _capacity.InCountAsync(meta.PartyId, ct);
            if (inCount >= meta.RosterCap)
            {
                return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Full, "The party is full right now.");
            }

            // A player already on the party's team is seated as a normal member; anyone else is a guest.
            var onTeam = await _db.TeamMemberships.AnyAsync(tm => tm.TeamId == meta.TeamId && tm.UserId == meta.UserId, ct);

            var member = await _db.PartyMembers.FirstOrDefaultAsync(m => m.PartyId == meta.PartyId && m.UserId == meta.UserId, ct);
            if (member is null)
            {
                _db.PartyMembers.Add(new PartyMember
                {
                    PartyId = meta.PartyId,
                    UserId = meta.UserId,
                    Status = PartyMemberStatus.In,
                    Role = PartyMemberRole.Member,
                    ViaMarket = !onTeam,
                });
            }
            else
            {
                member.Status = PartyMemberStatus.In;
                member.ViaMarket = !onTeam && member.Role != PartyMemberRole.Admin;
            }

            req.Status = MarketRequestStatus.Accepted;

            // "One event, one crew": revoke the joiner's other pending requests for this event and take
            // down their free-agent listing.
            var now = DateTime.UtcNow;
            await _db.MarketRequests
                .Where(r => r.UserId == meta.UserId && r.Id != requestId && r.Status == MarketRequestStatus.Pending
                    && _db.Parties.Any(p => p.Id == r.PartyId && p.EventId == meta.EventId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, MarketRequestStatus.Revoked)
                    .SetProperty(r => r.ModifiedDate, now), ct);
            await _db.MercenaryListings
                .Where(l => l.EventId == meta.EventId && l.UserId == meta.UserId)
                .ExecuteDeleteAsync(ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return null;
        });

        return failure ?? PartyResult<MarketRequestDto>.Ok(await LoadDtoAsync(requestId, ct));
    }

    // --- Decline / Revoke -----------------------------------------------------

    public async Task<PartyResult<MarketRequestDto>> DeclineAsync(Guid requestId, Guid actorUserId, CancellationToken ct = default)
    {
        var req = await _db.MarketRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null)
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.NotFound, "No such request.");
        }

        if (!await IsRecipientAsync(req, actorUserId, ct))
        {
            return PartyResult<MarketRequestDto>.Fail(PartyOutcome.Forbidden, "You can't answer this request.");
        }

        if (req.Status == MarketRequestStatus.Pending)
        {
            req.Status = MarketRequestStatus.Declined;
            await _db.SaveChangesAsync(ct);
        }

        return PartyResult<MarketRequestDto>.Ok(await LoadDtoAsync(requestId, ct));
    }

    public async Task<PartyResult> RevokeAsync(Guid requestId, Guid actorUserId, CancellationToken ct = default)
    {
        var req = await _db.MarketRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null)
        {
            return PartyResult.Fail(PartyOutcome.NotFound, "No such request.");
        }

        if (!await IsInitiatorAsync(req, actorUserId, ct))
        {
            return PartyResult.Fail(PartyOutcome.Forbidden, "You can't withdraw this request.");
        }

        if (req.Status == MarketRequestStatus.Pending)
        {
            req.Status = MarketRequestStatus.Revoked;
            await _db.SaveChangesAsync(ct);
        }

        return PartyResult.Ok();
    }

    // --- Inbox lists ----------------------------------------------------------

    public Task<PartyResult<PagedResult<MarketRequestDto>>> ListPartyApplicationsAsync(Guid partyId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default) =>
        ListPartyAsync(partyId, actorUserId, MarketRequestDirection.Application,
            [MarketRequestStatus.Pending], pagination, ct);

    public Task<PartyResult<PagedResult<MarketRequestDto>>> ListPartyInvitesAsync(Guid partyId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default) =>
        ListPartyAsync(partyId, actorUserId, MarketRequestDirection.Invite,
            [MarketRequestStatus.Pending, MarketRequestStatus.Declined], pagination, ct);

    private async Task<PartyResult<PagedResult<MarketRequestDto>>> ListPartyAsync(
        Guid partyId, Guid actorUserId, MarketRequestDirection direction, MarketRequestStatus[] statuses,
        PaginationRequest pagination, CancellationToken ct)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<PagedResult<MarketRequestDto>>.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult<PagedResult<MarketRequestDto>>.Fail(PartyOutcome.Forbidden, "Only a party admin can see the recruiting inbox.");
        }

        var query = _db.MarketRequests.AsNoTracking()
            .Where(r => r.PartyId == partyId && r.Direction == direction && statuses.Contains(r.Status));
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(ToDto)
            .ToListAsync(ct);

        return PartyResult<PagedResult<MarketRequestDto>>.Ok(
            new PagedResult<MarketRequestDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    public async Task<PagedResult<MyMarketRequestDto>> ListMineAsync(Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        // The caller's actionable items across events: invites to answer + pending applications.
        var query = _db.MarketRequests.AsNoTracking()
            .Where(r => r.UserId == actorUserId && r.Status == MarketRequestStatus.Pending);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(r => new MyMarketRequestDto(
                r.Id, r.PartyId, r.Party.Team.Name, r.Party.EventId, r.Party.Event.Name,
                r.Direction, r.Positions, r.Status, r.CreatedDate))
            .ToListAsync(ct);

        return new PagedResult<MyMarketRequestDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    // --- Direct-invite user search --------------------------------------------

    public async Task<PartyResult<PagedResult<MarketInvitableUserDto>>> SearchInvitableAsync(
        Guid partyId, Guid actorUserId, string query, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<PagedResult<MarketInvitableUserDto>>.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult<PagedResult<MarketInvitableUserDto>>.Fail(PartyOutcome.Forbidden, "Only a party admin can invite players.");
        }

        var term = (query ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return PartyResult<PagedResult<MarketInvitableUserDto>>.Ok(
                new PagedResult<MarketInvitableUserDto>([], 0, pagination.NormalizedSkip, pagination.NormalizedTake));
        }

        var eventId = access.Value.EventId;
        var pattern = $"%{term}%";
        var candidates = _db.PlayerProfiles.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.DisplayName, pattern) || EF.Functions.ILike(p.Handle, pattern));

        var total = await candidates.CountAsync(ct);
        var items = await candidates
            .OrderBy(p => p.DisplayName)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new MarketInvitableUserDto(
                p.UserId,
                p.Handle,
                p.DisplayName,
                p.Hometown,
                p.Avatar != null,
                _db.PartyMembers.Any(m => m.UserId == p.UserId && m.Status == PartyMemberStatus.In && m.Party.EventId == eventId)
                    ? MarketInviteRelation.Ineligible
                    : _db.MarketRequests.Any(r => r.PartyId == partyId && r.UserId == p.UserId
                        && r.Direction == MarketRequestDirection.Invite && r.Status == MarketRequestStatus.Pending)
                        ? MarketInviteRelation.Invited
                        : MarketInviteRelation.Invitable))
            .ToListAsync(ct);

        return PartyResult<PagedResult<MarketInvitableUserDto>>.Ok(
            new PagedResult<MarketInvitableUserDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    // --- Helpers --------------------------------------------------------------

    private Task<bool> HasActiveAsync(Guid partyId, Guid userId, CancellationToken ct) =>
        _db.MarketRequests.AnyAsync(r => r.PartyId == partyId && r.UserId == userId && r.Status == MarketRequestStatus.Pending, ct);

    private Task<MarketRequestDto> LoadDtoAsync(Guid requestId, CancellationToken ct) =>
        _db.MarketRequests.AsNoTracking().Where(r => r.Id == requestId).Select(ToDto).FirstAsync(ct);

    /// <summary>The recipient answers (accept/decline): a party admin for an application, the target for an invite.</summary>
    private async Task<bool> IsRecipientAsync(MarketRequest req, Guid actorUserId, CancellationToken ct)
    {
        if (req.Direction == MarketRequestDirection.Invite)
        {
            return req.UserId == actorUserId;
        }

        var access = await _guard.ResolveAsync(req.PartyId, actorUserId, ct);
        return access?.IsPartyAdmin == true;
    }

    /// <summary>The initiator withdraws: the applicant for an application, any party admin for an invite.</summary>
    private async Task<bool> IsInitiatorAsync(MarketRequest req, Guid actorUserId, CancellationToken ct)
    {
        if (req.Direction == MarketRequestDirection.Application)
        {
            return req.CreatedByUserId == actorUserId;
        }

        var access = await _guard.ResolveAsync(req.PartyId, actorUserId, ct);
        return access?.IsPartyAdmin == true;
    }
}
