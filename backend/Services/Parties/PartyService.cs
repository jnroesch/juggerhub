using System.Linq.Expressions;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Parties;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Events;
using JuggerHub.Services.Notifications;
using JuggerHub.Services.Teams;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuggerHub.Services.Parties;

/// <summary>
/// EF-Core-direct implementation of <see cref="IPartyService"/> (feature 016). Forming a party posts
/// the participation request (notification + email) to the team; applying reuses <see cref="EventCapacity"/>
/// to enter the team on the event via the existing feature-006 sign-up flow. Disband is a hard delete.
/// </summary>
public sealed class PartyService : IPartyService
{
    private readonly AppDbContext _db;
    private readonly PartyGuard _guard;
    private readonly PartyCapacity _capacity;
    private readonly EventCapacity _eventCapacity;
    private readonly TeamMembershipGuard _teamGuard;
    private readonly INotificationService _notifications;
    private readonly PartyEmailService _email;
    private readonly JuggerHub.Services.Chat.IChatConversationService _chat;

    public PartyService(
        AppDbContext db,
        PartyGuard guard,
        PartyCapacity capacity,
        EventCapacity eventCapacity,
        TeamMembershipGuard teamGuard,
        INotificationService notifications,
        PartyEmailService email,
        JuggerHub.Services.Chat.IChatConversationService chat)
    {
        _db = db;
        _guard = guard;
        _capacity = capacity;
        _eventCapacity = eventCapacity;
        _teamGuard = teamGuard;
        _notifications = notifications;
        _email = email;
        _chat = chat;
    }

    // --- Form -----------------------------------------------------------------

    public async Task<PartyResult<PartyDto>> FormAsync(Guid eventId, Guid teamId, string? message, Guid actorUserId, CancellationToken ct = default)
    {
        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.Status, e.ParticipantMode, e.RosterCap, e.EndsAt, e.Name })
            .FirstOrDefaultAsync(ct);
        if (ev is null)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.NotFound, "No such event.");
        }

        if (ev.ParticipantMode != ParticipantMode.Teams)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Invalid, "Parties are only for teams-only events.");
        }

        if (ev.Status == EventStatus.Cancelled || ev.EndsAt < DateTime.UtcNow)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Closed, "This event isn't accepting parties.");
        }

        var team = await _db.Teams.AsNoTracking()
            .Where(t => t.Id == teamId)
            .Select(t => new
            {
                t.Name,
                t.Slug,
                IsAdmin = t.Memberships.Any(m => m.UserId == actorUserId && m.Role == TeamRole.Admin),
            })
            .FirstOrDefaultAsync(ct);
        if (team is null)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.NotFound, "No such team.");
        }

        if (!team.IsAdmin)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.NotTeamAdmin, "Only a team admin can form a party.");
        }

        if (await _db.Parties.AnyAsync(p => p.TeamId == teamId && p.EventId == eventId, ct))
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Conflict, "This team already has a party for this event.");
        }

        var trimmed = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        var party = new Party
        {
            TeamId = teamId,
            EventId = eventId,
            RosterCap = ev.RosterCap ?? 8,
            Message = trimmed,
            Status = PartyStatus.Open,
            CreatedByUserId = actorUserId,
        };
        _db.Parties.Add(party);
        _db.PartyMembers.Add(new PartyMember
        {
            PartyId = party.Id,
            UserId = actorUserId,
            Status = PartyMemberStatus.In,
            Role = PartyMemberRole.Admin,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Conflict, "This team already has a party for this event.");
        }

        await PostRequestAsync(party.Id, teamId, actorUserId, eventId, ev.Name, team.Name, team.Slug, ct);

        var dto = await ProjectAsync(party.Id, actorUserId, ct);
        return PartyResult<PartyDto>.Ok(dto!);
    }

    /// <summary>Fan out the participation request (in-app + email) to every team member but the creator.</summary>
    private async Task PostRequestAsync(
        Guid partyId, Guid teamId, Guid actorUserId, Guid eventId, string eventName, string teamName, string teamSlug, CancellationToken ct)
    {
        var recipients = await _db.TeamMemberships.AsNoTracking()
            .Where(tm => tm.TeamId == teamId && tm.UserId != actorUserId)
            .Select(tm => new { tm.UserId, tm.User.Email, Name = tm.User.Profile!.DisplayName })
            .ToListAsync(ct);
        if (recipients.Count == 0)
        {
            return;
        }

        var payload = new { partyId, eventId, teamSlug, eventName, teamName };
        await _notifications.CreateManyAsync(
            recipients.Select(r => r.UserId).ToList(),
            NotificationType.PartyRequest,
            payload,
            actorUserId,
            dedupeKeyPrefix: $"party-request:{partyId}",
            ct);

        foreach (var r in recipients.Where(r => !string.IsNullOrEmpty(r.Email)))
        {
            await _email.SendPartyRequestEmailAsync(r.Email!, r.Name, teamName, eventName, teamSlug, eventId, ct);
        }
    }

    // --- Context (event page) -------------------------------------------------

    public async Task<PartyContextDto?> GetContextAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default)
    {
        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.ParticipantMode, e.RosterCap })
            .FirstOrDefaultAsync(ct);
        if (ev is null)
        {
            return null;
        }

        if (ev.ParticipantMode != ParticipantMode.Teams)
        {
            return new PartyContextDto(ev.ParticipantMode, null, []);
        }

        // Every team the caller belongs to (member or admin) with its party for this event, if any.
        var teams = await _db.TeamMemberships.AsNoTracking()
            .Where(tm => tm.UserId == actorUserId)
            .Select(tm => new
            {
                tm.TeamId,
                tm.Team.Name,
                tm.Team.Slug,
                IsAdmin = tm.Role == TeamRole.Admin,
                Party = _db.Parties
                    .Where(p => p.TeamId == tm.TeamId && p.EventId == eventId)
                    .Select(p => new
                    {
                        p.Id,
                        p.Status,
                        p.RosterCap,
                        InCount = p.Members.Count(m => m.Status == PartyMemberStatus.In),
                        MyRole = p.Members.Where(m => m.UserId == actorUserId).Select(m => (PartyMemberRole?)m.Role).FirstOrDefault(),
                        MyStatus = p.Members.Where(m => m.UserId == actorUserId).Select(m => (PartyMemberStatus?)m.Status).FirstOrDefault(),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        // Surface a team when the caller can form a party for it (admin, no party yet) or when a
        // party already exists there (so members see a "view party" card instead of a form button).
        var list = teams
            .Where(t => t.IsAdmin || t.Party is not null)
            .Select(t => new PartyContextTeamDto(
                t.TeamId,
                t.Name,
                t.Slug,
                t.IsAdmin,
                t.Party?.Id,
                CanForm: t.IsAdmin && t.Party is null,
                MyState: t.Party is null
                    ? PartyViewerState.None
                    : ResolveState(t.Party.MyRole, t.Party.MyStatus, isTeamMember: true),
                InCount: t.Party?.InCount,
                RosterCap: t.Party?.RosterCap,
                PartyStatus: t.Party?.Status))
            .ToList();

        return new PartyContextDto(ParticipantMode.Teams, ev.RosterCap, list);
    }

    // --- Detail ---------------------------------------------------------------

    public async Task<PartyDto?> GetDetailAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        // Team members see the party; a marketplace guest (In but not on the team, feature 017) is crew
        // and may view the hub too. Outsiders get 404 (a party's existence never leaks).
        if (access is null || !(access.Value.IsTeamMember || access.Value.IsCrew))
        {
            return null;
        }

        return await ProjectAsync(partyId, actorUserId, ct);
    }

    // --- Team requests (team space) -------------------------------------------

    public async Task<PagedResult<PartyRequestCardDto>?> ListTeamRequestsAsync(string slug, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _teamGuard.ResolveAsync(slug, actorUserId, ct);
        if (access is null || !access.Value.IsMember)
        {
            return null; // 404 — member-gated.
        }

        var teamId = access.Value.TeamId;
        var query = _db.Parties.AsNoTracking().Where(p => p.TeamId == teamId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new
            {
                p.Id,
                p.EventId,
                EventName = p.Event.Name,
                EventType = p.Event.Type,
                p.Event.StartsAt,
                p.Event.EndsAt,
                p.RosterCap,
                p.Message,
                p.Status,
                InCount = p.Members.Count(m => m.Status == PartyMemberStatus.In),
                MyRole = p.Members.Where(m => m.UserId == actorUserId).Select(m => (PartyMemberRole?)m.Role).FirstOrDefault(),
                MyStatus = p.Members.Where(m => m.UserId == actorUserId).Select(m => (PartyMemberStatus?)m.Status).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var cards = items.Select(p => new PartyRequestCardDto(
            p.Id, p.EventId, p.EventName, p.EventType, p.StartsAt, p.EndsAt,
            p.InCount, p.RosterCap, p.Message,
            ResolveState(p.MyRole, p.MyStatus, isTeamMember: true),
            IsFull: p.InCount >= p.RosterCap,
            p.Status)).ToList();

        return new PagedResult<PartyRequestCardDto>(cards, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    // --- Apply ----------------------------------------------------------------

    public async Task<PartyResult<PartyDto>> ApplyAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Forbidden, "Only a party admin can apply to the event.");
        }

        if (!access.Value.IsEventOpen)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Closed, "This event isn't accepting entries.");
        }

        if (access.Value.Status == PartyStatus.Applied)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Conflict, "This party has already been applied.");
        }

        var eventId = access.Value.EventId;
        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.IsPaid, e.ParticipationLimit })
            .FirstAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _eventCapacity.LockEventRowAsync(eventId, ct);
        var occupied = await _eventCapacity.OccupiedCountAsync(eventId, ct);
        var status = occupied < ev.ParticipationLimit
            ? (ev.IsPaid ? SignupStatus.AwaitingApproval : SignupStatus.Joined)
            : SignupStatus.Waitlisted;

        var signup = new EventSignup { EventId = eventId, TeamId = access.Value.TeamId, Status = status };
        _db.EventSignups.Add(signup);

        var party = await _db.Parties.FirstAsync(p => p.Id == partyId, ct);
        party.EventSignupId = signup.Id;
        party.Status = PartyStatus.Applied;

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Conflict, "This team is already entered on the event.");
        }

        var dto = await ProjectAsync(partyId, actorUserId, ct);
        return PartyResult<PartyDto>.Ok(dto!);
    }

    // --- Withdraw -------------------------------------------------------------

    public async Task<PartyResult<PartyDto>> WithdrawAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Forbidden, "Only a party admin can withdraw.");
        }

        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Id == partyId, ct);
        if (party is null)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.NotFound);
        }

        if (party.Status != PartyStatus.Applied || party.EventSignupId is not Guid signupId)
        {
            return PartyResult<PartyDto>.Fail(PartyOutcome.Conflict, "This party isn't currently entered on the event.");
        }

        // Clear the FK reference first, then delete the signup — the FK is Restrict, so the row
        // can't be deleted while the party still points at it.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        party.EventSignupId = null;
        party.Status = PartyStatus.Open;
        await _db.SaveChangesAsync(ct);
        await _db.EventSignups.Where(s => s.Id == signupId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        var dto = await ProjectAsync(partyId, actorUserId, ct);
        return PartyResult<PartyDto>.Ok(dto!);
    }

    // --- Disband --------------------------------------------------------------

    public async Task<PartyResult> DisbandAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult.Fail(PartyOutcome.Forbidden, "Only a party admin can disband the party.");
        }

        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Id == partyId, ct);
        if (party is null)
        {
            return PartyResult.Fail(PartyOutcome.NotFound);
        }

        // Hard-delete the party first (members, news, and invitations cascade), which removes the
        // Restrict FK reference, then delete the applied signup. The team, its roster, and badges
        // are untouched. Wrapped in a transaction so the event entry can't be orphaned.
        var signupId = party.EventSignupId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Archive the party's chat BEFORE the party goes (feature 019, data-model R3a). Order matters
        // twice over: PartyMembers cascade away with the party, and the chat DERIVES its membership
        // from them — so archiving afterwards would leave a conversation nobody can read. It also
        // clears the chat's Restrict FK, which would otherwise block this delete outright.
        await _chat.ArchiveForPartyAsync(partyId, ct);

        _db.Parties.Remove(party);
        await _db.SaveChangesAsync(ct);
        if (signupId is Guid sid)
        {
            await _db.EventSignups.Where(s => s.Id == sid).ExecuteDeleteAsync(ct);
        }

        await tx.CommitAsync(ct);
        return PartyResult.Ok();
    }

    // --- Helpers --------------------------------------------------------------

    /// <summary>Builds the full <see cref="PartyDto"/> (counts, viewer state, readiness) in one query.</summary>
    private async Task<PartyDto?> ProjectAsync(Guid partyId, Guid actorUserId, CancellationToken ct)
    {
        var p = await _db.Parties.AsNoTracking()
            .Where(x => x.Id == partyId)
            .Select(x => new
            {
                x.Id,
                x.EventId,
                EventName = x.Event.Name,
                EventType = x.Event.Type,
                x.Event.StartsAt,
                x.Event.EndsAt,
                x.TeamId,
                TeamSlug = x.Team.Slug,
                TeamName = x.Team.Name,
                x.RosterCap,
                x.Status,
                x.Message,
                AppliedGroup = x.EventSignup != null ? (SignupStatus?)x.EventSignup.Status : null,
                // Team members who are In (drives the "no response" derivation — guests are not team members).
                TeamInCount = x.Members.Count(m => m.Status == PartyMemberStatus.In && x.Team.Memberships.Any(tm => tm.UserId == m.UserId)),
                // Displayed crew fill = team members + guests (feature 017), In. Deduped by the single predicate.
                InCount = x.Members.Count(m => m.Status == PartyMemberStatus.In && (m.ViaMarket || x.Team.Memberships.Any(tm => tm.UserId == m.UserId))),
                DeclinedCount = x.Members.Count(m => m.Status == PartyMemberStatus.Declined && x.Team.Memberships.Any(tm => tm.UserId == m.UserId)),
                TeamMemberCount = x.Team.Memberships.Count(),
                MyRole = x.Members.Where(m => m.UserId == actorUserId).Select(m => (PartyMemberRole?)m.Role).FirstOrDefault(),
                MyStatus = x.Members.Where(m => m.UserId == actorUserId).Select(m => (PartyMemberStatus?)m.Status).FirstOrDefault(),
                IsTeamMember = x.Team.Memberships.Any(tm => tm.UserId == actorUserId),
            })
            .FirstOrDefaultAsync(ct);
        if (p is null)
        {
            return null;
        }

        // "No response" is team-only (guests never sit in this group); the crew fill (p.InCount)
        // includes guests, so derive unanswered from the team-only In count.
        var noResponse = Math.Max(0, p.TeamMemberCount - p.TeamInCount - p.DeclinedCount);
        var readiness = new PartyReadinessDto(
            EnoughToFieldTeam: p.InCount >= 5,
            SpotsOpen: Math.Max(0, p.RosterCap - p.InCount),
            Unanswered: noResponse);

        return new PartyDto(
            p.Id, p.EventId, p.EventName, p.EventType, p.StartsAt, p.EndsAt,
            p.TeamId, p.TeamSlug, p.TeamName, p.RosterCap,
            p.InCount, p.DeclinedCount, noResponse,
            IsFull: p.InCount >= p.RosterCap,
            p.Status,
            ResolveState(p.MyRole, p.MyStatus, p.IsTeamMember),
            p.MyRole,
            p.Message,
            p.AppliedGroup,
            readiness);
    }

    internal static PartyViewerState ResolveState(PartyMemberRole? role, PartyMemberStatus? status, bool isTeamMember) =>
        role == PartyMemberRole.Admin ? PartyViewerState.Admin
        : status == PartyMemberStatus.In ? PartyViewerState.In
        : status == PartyMemberStatus.Declined ? PartyViewerState.Declined
        : isTeamMember ? PartyViewerState.NoResponse
        : PartyViewerState.None;

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
