using System.Text.Json;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Home;
using JuggerHub.Entities;
using JuggerHub.Services.Trainings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Home;

/// <summary>
/// EF-Core-direct implementation of <see cref="IHomeService"/> (feature 008, reshaped by feature 025
/// around participation + action). Composes the dashboard from existing data; every read is projected
/// + <c>AsNoTracking</c> and scoped to the caller. Occupied spots = Joined + AwaitingApproval (mirrors
/// the events EventCapacity rule). Sections: Needs you (actionable) → Up next (unified events +
/// trainings) → News (team/event/party) → What's going on (passive activity).
/// </summary>
public sealed class HomeService : IHomeService
{
    private readonly AppDbContext _db;
    private readonly HomeOptions _options;
    private readonly ITrainingResponseService _trainingResponses;

    public HomeService(AppDbContext db, IOptions<HomeOptions> options, ITrainingResponseService trainingResponses)
    {
        _db = db;
        _options = options.Value;
        _trainingResponses = trainingResponses;
    }

    public async Task<HomeDto> GetHomeAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var myTeamIds = await MyTeamIdsAsync(userId, ct);

        var viewer = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new ViewerSummaryDto(p.DisplayName, p.Handle, p.Avatar != null))
            .FirstOrDefaultAsync(ct)
            ?? new ViewerSummaryDto("there", string.Empty, false);

        var teams = await MyTeamsQuery(userId).Take(_options.TeamsCap).ToListAsync(ct);

        // Needs you — actionable items awaiting the viewer (feature 025, US1).
        var needsYou = await LoadNeedsYouAsync(userId, myTeamIds, now, ct);

        // Up next — unified participation agenda (events; trainings folded in US2), capped.
        var (upNext, _) = await LoadUpNextAsync(userId, myTeamIds, 0, _options.UpNextCap, now, ct);

        // Open to everyone — only for new-player (no-team) viewers.
        var openToEveryone = myTeamIds.Count == 0
            ? await LoadOpenToEveryoneAsync(userId, _options.OpenCap, now, ct)
            : [];

        // News — team + event sources now (party added in US3), merged newest-first, capped.
        var (news, _) = await LoadNewsAsync(userId, myTeamIds, 0, _options.NewsCap, ct);

        // What's going on — passive activity (feature 025, US4). Team-scoped ambient awareness;
        // empty for the no-team variant (FR-028).
        var activity = myTeamIds.Count == 0
            ? new List<ActivityEntryDto>()
            : await LoadActivityAsync(userId, myTeamIds, ct);

        return new HomeDto(viewer, teams, needsYou, upNext, openToEveryone, news, activity);
    }

    public async Task<PagedResult<AgendaItemDto>> ListUpNextAsync(
        Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var myTeamIds = await MyTeamIdsAsync(userId, ct);
        var (items, total) = await LoadUpNextAsync(
            userId, myTeamIds, pagination.NormalizedSkip, pagination.NormalizedTake, now, ct);
        return new PagedResult<AgendaItemDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<PagedResult<HomeNewsDto>> ListNewsAsync(
        Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var myTeamIds = await MyTeamIdsAsync(userId, ct);
        var (items, total) = await LoadNewsAsync(
            userId, myTeamIds, pagination.NormalizedSkip, pagination.NormalizedTake, ct);
        return new PagedResult<HomeNewsDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<PagedResult<MyTeamDto>> ListMyTeamsAsync(
        Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var query = MyTeamsQuery(userId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(pagination.NormalizedSkip).Take(pagination.NormalizedTake)
            .ToListAsync(ct);
        return new PagedResult<MyTeamDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    // --- Helpers --------------------------------------------------------------

    private Task<List<Guid>> MyTeamIdsAsync(Guid userId, CancellationToken ct) =>
        _db.TeamMemberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.TeamId)
            .ToListAsync(ct);

    /// <summary>Memberships newest-first (most recent join is the default "primary" for routing).</summary>
    private IQueryable<MyTeamDto> MyTeamsQuery(Guid userId) =>
        _db.TeamMemberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.JoinedDate).ThenBy(m => m.TeamId)
            .Select(m => new MyTeamDto(m.Team.Slug, m.Team.Name, m.Role));

    /// <summary>
    /// "Needs you" — the actionable items awaiting the viewer's response, aggregated from each
    /// authoritative source domain (never the notification display-cache, so a read/stale notification
    /// can never leave a ghost action). Feature 025: pending targeted team invites + party co-admin
    /// invites (token action), party participation requests where the viewer has not answered, pending
    /// marketplace invites/applications, and near-window un-answered trainings. Capped.
    /// </summary>
    private async Task<List<NeedsYouItemDto>> LoadNeedsYouAsync(
        Guid userId, List<Guid> myTeamIds, DateTime now, CancellationToken ct)
    {
        var cap = _options.NeedsYouCap;

        var teamInvites = await _db.TeamInvitations.AsNoTracking()
            .Where(i => i.Kind == InvitationKind.Targeted && i.Status == InvitationStatus.Pending
                && i.ExpiresDate > now && i.TargetUserId == userId)
            .OrderByDescending(i => i.CreatedDate)
            .Take(cap)
            .Select(i => new NeedsYouItemDto(
                NeedsYouKind.TeamInvite, i.Token,
                i.Team.Name + " invited you", "to join the team", i.Team.Slug, i.CreatedDate))
            .ToListAsync(ct);

        var coAdminInvites = await _db.PartyAdminInvitations.AsNoTracking()
            .Where(i => i.Kind == InvitationKind.Targeted && i.Status == InvitationStatus.Pending
                && i.ExpiresDate > now && i.TargetUserId == userId)
            .OrderByDescending(i => i.CreatedDate)
            .Take(cap)
            .Select(i => new NeedsYouItemDto(
                NeedsYouKind.PartyCoAdminInvite, i.Token,
                "Co-admin a party for " + i.Party.Event.Name, i.Party.Team.Name,
                i.Party.EventId.ToString(), i.CreatedDate))
            .ToListAsync(ct);

        // Party participation requests: a party of one of the viewer's teams, event upcoming, with no
        // PartyMember row for the viewer yet ("no response" is derived — feature 016).
        var partyRequests = myTeamIds.Count == 0
            ? new List<NeedsYouItemDto>()
            : await _db.Parties.AsNoTracking()
                .Where(p => myTeamIds.Contains(p.TeamId)
                    && p.Event.Status == EventStatus.Published && p.Event.EndsAt >= now
                    && !p.Members.Any(m => m.UserId == userId))
                .OrderByDescending(p => p.CreatedDate)
                .Take(cap)
                .Select(p => new NeedsYouItemDto(
                    NeedsYouKind.PartyRequest, p.Id.ToString(),
                    p.Team.Name + " is fielding a party", p.Event.Name, p.EventId.ToString(), p.CreatedDate))
                .ToListAsync(ct);

        var market = await _db.MarketRequests.AsNoTracking()
            .Where(r => r.UserId == userId && r.Status == MarketRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedDate)
            .Take(cap)
            .Select(r => new NeedsYouItemDto(
                r.Direction == MarketRequestDirection.Invite ? NeedsYouKind.MarketInvite : NeedsYouKind.MarketApplication,
                r.Id.ToString(),
                r.Direction == MarketRequestDirection.Invite ? r.Party.Team.Name + " want you" : "You applied to " + r.Party.Team.Name,
                r.Party.Event.Name, r.Party.EventId.ToString(), r.CreatedDate))
            .ToListAsync(ct);

        // Trainings are intentionally NOT here (feature 025 revision): "Needs you" is invites and
        // requests only. Training RSVP lives inline in "Up next" — keeping the top section free of
        // agenda RSVP avoids duplicating a training across both sections.
        return teamInvites
            .Concat(coAdminInvites)
            .Concat(partyRequests)
            .Concat(market)
            .OrderByDescending(x => x.OccurredAt)
            .Take(cap)
            .ToList();
    }

    /// <summary>
    /// "What's going on" — a passive, read-only activity feed (feature 025). Derived on read from
    /// domain rows for the participation/social signals (a teammate signed up, a new team member, a
    /// badge award, a party member joined) plus the viewer's own passive notification rows for pure
    /// state-changes (role change, training reschedule/cancel). No fan-out writes, no new table.
    /// Merged newest-first and capped. All entries are scoped to the viewer or their teams/parties.
    /// </summary>
    private async Task<List<ActivityEntryDto>> LoadActivityAsync(Guid userId, List<Guid> myTeamIds, CancellationToken ct)
    {
        var cap = _options.ActivityCap;

        var teammateSignups = await _db.EventSignups.AsNoTracking()
            .Where(s => s.UserId != null && s.UserId != userId
                && s.Event.ParticipantMode == ParticipantMode.Individuals
                && _db.TeamMemberships.Any(m => m.UserId == s.UserId && myTeamIds.Contains(m.TeamId)))
            .OrderByDescending(s => s.CreatedDate)
            .Take(cap)
            .Select(s => new ActivityEntryDto(
                ActivityKind.TeammateJoinedEvent,
                (_db.PlayerProfiles.Where(p => p.UserId == s.UserId).Select(p => p.DisplayName).FirstOrDefault() ?? "A teammate")
                    + " signed up for " + s.Event.Name,
                s.EventId.ToString(),
                s.CreatedDate))
            .ToListAsync(ct);

        var newMembers = await _db.TeamMemberships.AsNoTracking()
            .Where(m => myTeamIds.Contains(m.TeamId) && m.UserId != userId)
            .OrderByDescending(m => m.JoinedDate)
            .Take(cap)
            .Select(m => new ActivityEntryDto(
                ActivityKind.NewTeamMember,
                (_db.PlayerProfiles.Where(p => p.UserId == m.UserId).Select(p => p.DisplayName).FirstOrDefault() ?? "Someone")
                    + " joined " + m.Team.Name,
                m.Team.Slug,
                m.JoinedDate))
            .ToListAsync(ct);

        var partyJoins = await _db.PartyMembers.AsNoTracking()
            .Where(pm => pm.Status == PartyMemberStatus.In && pm.UserId != userId && myTeamIds.Contains(pm.Party.TeamId))
            .OrderByDescending(pm => pm.CreatedDate)
            .Take(cap)
            .Select(pm => new ActivityEntryDto(
                ActivityKind.PartyMemberJoined,
                (_db.PlayerProfiles.Where(p => p.UserId == pm.UserId).Select(p => p.DisplayName).FirstOrDefault() ?? "Someone")
                    + " joined the party for " + pm.Party.Event.Name,
                pm.Party.EventId.ToString(),
                pm.CreatedDate))
            .ToListAsync(ct);

        // Badges to the viewer or a teammate; fetch raw for the "You"/name summary split.
        var badgeRaw = await _db.BadgeAwards.AsNoTracking()
            .Where(b => b.Status == AwardStatus.Active && b.PlayerProfileId != null
                && _db.PlayerProfiles.Any(p => p.Id == b.PlayerProfileId
                    && (p.UserId == userId
                        || _db.TeamMemberships.Any(m => m.UserId == p.UserId && myTeamIds.Contains(m.TeamId)))))
            .OrderByDescending(b => b.EarnedAt)
            .Take(cap)
            .Select(b => new
            {
                b.EarnedAt,
                BadgeName = b.Definition.Name,
                Handle = _db.PlayerProfiles.Where(p => p.Id == b.PlayerProfileId).Select(p => p.Handle).FirstOrDefault(),
                Name = _db.PlayerProfiles.Where(p => p.Id == b.PlayerProfileId).Select(p => p.DisplayName).FirstOrDefault(),
                IsMine = _db.PlayerProfiles.Any(p => p.Id == b.PlayerProfileId && p.UserId == userId),
            })
            .ToListAsync(ct);
        var badges = badgeRaw.Select(b => new ActivityEntryDto(
            ActivityKind.BadgeAwarded,
            (b.IsMine ? "You" : (b.Name ?? "A teammate")) + " earned the " + b.BadgeName + " badge",
            b.Handle,
            b.EarnedAt)).ToList();

        // Pure state-changes from the viewer's own passive notification rows (role change, training edit).
        var notifRaw = await _db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == userId
                && (n.Type == NotificationType.TeamRoleChanged || n.Type == NotificationType.TrainingUpdated))
            .OrderByDescending(n => n.CreatedDate)
            .Take(cap)
            .Select(n => new { n.Type, n.Payload, n.CreatedDate })
            .ToListAsync(ct);
        var stateChanges = notifRaw.Select(n => StateChangeEntry(n.Type, n.Payload, n.CreatedDate)).ToList();

        return teammateSignups
            .Concat(newMembers)
            .Concat(partyJoins)
            .Concat(badges)
            .Concat(stateChanges)
            .OrderByDescending(a => a.OccurredAt)
            .Take(cap)
            .ToList();
    }

    /// <summary>Render a passive state-change notification into an activity entry (payload is camelCase JSON).</summary>
    private static ActivityEntryDto StateChangeEntry(NotificationType type, string payload, DateTime when)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
        var root = doc.RootElement;
        string Str(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

        if (type == NotificationType.TeamRoleChanged)
        {
            var team = Str("teamName");
            var role = Str("newRole");
            var slug = Str("teamSlug");
            return new ActivityEntryDto(
                ActivityKind.RoleChanged,
                string.IsNullOrEmpty(team) ? "Your team role changed" : $"Your role in {team} is now {role}",
                string.IsNullOrEmpty(slug) ? null : slug,
                when);
        }

        var name = Str("trainingName");
        var kind = Str("kind");
        var sessionId = Str("sessionId");
        var summary = kind == "cancelled"
            ? (string.IsNullOrEmpty(name) ? "A training was cancelled" : $"{name} was cancelled")
            : (string.IsNullOrEmpty(name) ? "A training was updated" : $"{name} was updated");
        return new ActivityEntryDto(
            ActivityKind.TrainingChanged,
            summary,
            string.IsNullOrEmpty(sessionId) ? null : sessionId,
            when);
    }

    /// <summary>
    /// The union of the viewer's individuals-mode sign-ups and their teams' team-mode entries,
    /// soonest-first, de-duped by event, past/cancelled excluded. A bounded window is read from
    /// each source, merged and paged in memory (research §2). Feature 025 will fold trainings in (US2).
    /// </summary>
    private async Task<(List<AgendaItemDto> Items, int Total)> LoadUpNextAsync(
        Guid userId, List<Guid> myTeamIds, int skip, int take, DateTime now, CancellationToken ct)
    {
        var window = skip + take;

        var personal = await _db.EventSignups.AsNoTracking()
            .Where(s => s.UserId == userId && s.Event.Status == EventStatus.Published && s.Event.EndsAt >= now)
            .OrderBy(s => s.Event.StartsAt).ThenBy(s => s.EventId)
            .Take(window)
            .Select(s => new HomeProjections.UpNextRaw(
                s.EventId, s.Event.Name, s.Event.Type, s.Event.CustomTypeLabel,
                s.Event.StartsAt, s.Event.EndsAt, s.Event.City, s.Event.VenueName, s.Event.Location,
                s.Event.ParticipationLimit,
                s.Event.Signups.Count(x => x.Status == SignupStatus.Joined || x.Status == SignupStatus.AwaitingApproval),
                ParticipantMode.Individuals, s.Id, s.Status, null, null))
            .ToListAsync(ct);

        var teamRaw = myTeamIds.Count == 0
            ? new List<HomeProjections.UpNextRaw>()
            : await _db.EventSignups.AsNoTracking()
                .Where(s => s.TeamId != null && myTeamIds.Contains(s.TeamId!.Value)
                    && s.Event.Status == EventStatus.Published && s.Event.EndsAt >= now)
                .OrderBy(s => s.Event.StartsAt).ThenBy(s => s.EventId)
                .Take(window + 50) // headroom before multi-team de-dup
                .Select(s => new HomeProjections.UpNextRaw(
                    s.EventId, s.Event.Name, s.Event.Type, s.Event.CustomTypeLabel,
                    s.Event.StartsAt, s.Event.EndsAt, s.Event.City, s.Event.VenueName, s.Event.Location,
                    s.Event.ParticipationLimit,
                    s.Event.Signups.Count(x => x.Status == SignupStatus.Joined || x.Status == SignupStatus.AwaitingApproval),
                    ParticipantMode.Teams, null, null, s.Team!.Slug, s.Team.Name))
                .ToListAsync(ct);

        // De-dup team rows by event (a viewer on two teams that both entered one event).
        var team = teamRaw
            .GroupBy(r => r.EventId)
            .Select(g => g.OrderBy(r => r.TeamName).First());

        var eventItems = personal.Concat(team)
            .GroupBy(r => r.EventId).Select(g => g.First()) // personal/team can't collide, but stay safe
            .Select(HomeProjections.ToItem)
            .ToList();

        // Trainings folded into the participation agenda (feature 025 revision): ALL upcoming
        // sessions appear here (answered or not) with an inline RSVP — trainings no longer surface in
        // "Needs you", so there is nothing to exclude.
        var trainingTake = Math.Min(window + 50, 100);
        var agenda = await _trainingResponses.GetMyAgendaAsync(userId, new PaginationRequest { Take = trainingTake }, ct);
        var trainingItems = agenda.Items
            .Select(HomeProjections.ToItem)
            .ToList();

        var merged = eventItems.Concat(trainingItems)
            .OrderBy(i => i.StartsAt).ThenBy(i => i.Id)
            .ToList();

        // Total across sources (mode-exclusive events + folded trainings within the read window).
        var personalCount = await _db.EventSignups.AsNoTracking()
            .CountAsync(s => s.UserId == userId && s.Event.Status == EventStatus.Published && s.Event.EndsAt >= now, ct);
        var teamCount = myTeamIds.Count == 0 ? 0 : await _db.EventSignups.AsNoTracking()
            .Where(s => s.TeamId != null && myTeamIds.Contains(s.TeamId!.Value)
                && s.Event.Status == EventStatus.Published && s.Event.EndsAt >= now)
            .Select(s => s.EventId).Distinct().CountAsync(ct);

        var page = merged.Skip(skip).Take(take).ToList();
        return (page, personalCount + teamCount + trainingItems.Count);
    }

    /// <summary>Upcoming published individuals-mode events the viewer has not joined (RSVP prompts).</summary>
    private async Task<List<AgendaItemDto>> LoadOpenToEveryoneAsync(
        Guid userId, int cap, DateTime now, CancellationToken ct)
    {
        var raw = await _db.Events.AsNoTracking()
            .Where(e => e.ParticipantMode == ParticipantMode.Individuals
                && e.Status == EventStatus.Published && e.EndsAt >= now
                && !e.Signups.Any(s => s.UserId == userId))
            .OrderBy(e => e.StartsAt).ThenBy(e => e.Id)
            .Take(cap)
            .Select(e => new HomeProjections.UpNextRaw(
                e.Id, e.Name, e.Type, e.CustomTypeLabel, e.StartsAt, e.EndsAt,
                e.City, e.VenueName, e.Location, e.ParticipationLimit,
                e.Signups.Count(x => x.Status == SignupStatus.Joined || x.Status == SignupStatus.AwaitingApproval),
                ParticipantMode.Individuals, null, null, null, null))
            .ToListAsync(ct);

        return raw.Select(HomeProjections.ToItem).ToList();
    }

    /// <summary>
    /// Team news (member teams) + event news (connected events), merged newest-first. Feature 025
    /// adds party news (US3).
    /// </summary>
    private async Task<(List<HomeNewsDto> Items, int Total)> LoadNewsAsync(
        Guid userId, List<Guid> myTeamIds, int skip, int take, CancellationToken ct)
    {
        var window = _options.NewsWindow;

        var teamNews = myTeamIds.Count == 0
            ? new List<HomeProjections.NewsRaw>()
            : await _db.TeamNewsPosts.AsNoTracking()
                .Where(n => myTeamIds.Contains(n.TeamId))
                .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id)
                .Take(window)
                .Select(n => new HomeProjections.NewsRaw("team", n.Team.Name, n.Team.Slug, n.Body, n.CreatedDate, n.Id))
                .ToListAsync(ct);

        // Connected events: viewer/team sign-up OR viewer admins (EXISTS subqueries — no unbounded read).
        var eventNews = await _db.EventNewsPosts.AsNoTracking()
            .Where(n =>
                _db.EventSignups.Any(s => s.EventId == n.EventId && s.UserId == userId)
                || _db.EventSignups.Any(s => s.EventId == n.EventId && s.TeamId != null && myTeamIds.Contains(s.TeamId!.Value))
                || _db.EventAdmins.Any(a => a.EventId == n.EventId && a.UserId == userId))
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id)
            .Take(window)
            .Select(n => new HomeProjections.NewsRaw("event", n.Event.Name, n.Event.Id.ToString(), n.Body, n.CreatedDate, n.Id))
            .ToListAsync(ct);

        // Party news (feature 025): posts in a party the viewer is currently an `In` member of — this
        // includes market guests who are not on the party's team, so it is gated on membership, not
        // myTeamIds. Link target is the party's event.
        var partyNews = await _db.PartyNewsPosts.AsNoTracking()
            .Where(n => _db.PartyMembers.Any(m => m.PartyId == n.PartyId && m.UserId == userId && m.Status == PartyMemberStatus.In))
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id)
            .Take(window)
            .Select(n => new HomeProjections.NewsRaw(
                "party", n.Party.Team.Name + " @ " + n.Party.Event.Name, n.Party.EventId.ToString(), n.Body, n.CreatedDate, n.Id))
            .ToListAsync(ct);

        var merged = HomeNewsMerge.Merge(teamNews, eventNews, partyNews);
        var page = merged.Skip(skip).Take(take).ToList();
        return (page, merged.Count);
    }
}
