using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Home;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Home;

/// <summary>
/// EF-Core-direct implementation of <see cref="IHomeService"/> (feature 008). Composes the
/// dashboard from existing data; every read is projected + <c>AsNoTracking</c> and scoped to
/// the caller. Occupied spots = Joined + AwaitingApproval (mirrors the events EventCapacity rule).
/// </summary>
public sealed class HomeService : IHomeService
{
    private readonly AppDbContext _db;
    private readonly HomeOptions _options;

    public HomeService(AppDbContext db, IOptions<HomeOptions> options)
    {
        _db = db;
        _options = options.Value;
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

        // Up next (own individuals sign-ups + teams' team-mode entries), capped.
        var (upNext, _) = await LoadUpNextAsync(userId, myTeamIds, 0, _options.UpNextCap, now, ct);

        // Open to everyone — only for new-player (no-team) viewers.
        var openToEveryone = myTeamIds.Count == 0
            ? await LoadOpenToEveryoneAsync(userId, _options.OpenCap, now, ct)
            : [];

        // Your teams — aggregated recent activity (from team news today), newest-first.
        var teamsActivity = myTeamIds.Count == 0
            ? new List<TeamActivityDto>()
            : await _db.TeamNewsPosts.AsNoTracking()
                .Where(n => myTeamIds.Contains(n.TeamId))
                .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id)
                .Take(_options.ActivityCap)
                .Select(n => new TeamActivityDto(n.Team.Slug, n.Team.Name, n.Body, n.CreatedDate))
                .ToListAsync(ct);

        // News — team + event sources, merged newest-first, capped.
        var (news, _) = await LoadNewsAsync(userId, myTeamIds, 0, _options.NewsCap, ct);

        // Tournaments — upcoming published Tournament events, soonest-first.
        var tournaments = await _db.Events.AsNoTracking()
            .Where(e => e.Type == EventType.Tournament && e.Status == EventStatus.Published && e.EndsAt >= now)
            .OrderBy(e => e.StartsAt).ThenBy(e => e.Id)
            .Take(_options.TournamentCap)
            .Select(e => new TournamentCardDto(
                e.Id,
                e.Name,
                !string.IsNullOrEmpty(e.City) ? e.City! : (!string.IsNullOrEmpty(e.VenueName) ? e.VenueName! : e.Location),
                e.StartsAt,
                e.ParticipationLimit - e.Signups.Count(s => s.Status == SignupStatus.Joined || s.Status == SignupStatus.AwaitingApproval)))
            .ToListAsync(ct);
        // Clamp spots (SQL keeps the raw difference; never show negative).
        tournaments = tournaments
            .Select(t => t with { SpotsRemaining = Math.Max(t.SpotsRemaining, 0) })
            .ToList();

        // Snapshots — one per team, with the soonest upcoming fixture (no record).
        var snapshots = myTeamIds.Count == 0
            ? new List<TeamSnapshotDto>()
            : await _db.Teams.AsNoTracking()
                .Where(t => myTeamIds.Contains(t.Id))
                .OrderBy(t => t.Name)
                .Take(_options.TeamsCap)
                .Select(t => new TeamSnapshotDto(
                    t.Slug,
                    t.Name,
                    _db.EventSignups.AsNoTracking()
                        .Where(s => s.TeamId == t.Id && s.Event.Status == EventStatus.Published && s.Event.EndsAt >= now)
                        .OrderBy(s => s.Event.StartsAt)
                        .Select(s => new NextFixtureDto(s.EventId, s.Event.Name, s.Event.StartsAt))
                        .FirstOrDefault()))
                .ToListAsync(ct);

        return new HomeDto(viewer, teams, upNext, openToEveryone, teamsActivity, news, tournaments, snapshots);
    }

    public async Task<PagedResult<UpNextItemDto>> ListUpNextAsync(
        Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var myTeamIds = await MyTeamIdsAsync(userId, ct);
        var (items, total) = await LoadUpNextAsync(
            userId, myTeamIds, pagination.NormalizedSkip, pagination.NormalizedTake, now, ct);
        return new PagedResult<UpNextItemDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
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
    /// The union of the viewer's individuals-mode sign-ups and their teams' team-mode entries,
    /// soonest-first, de-duped by event, past/cancelled excluded. A bounded window is read from
    /// each source, merged and paged in memory (research §2).
    /// </summary>
    private async Task<(List<UpNextItemDto> Items, int Total)> LoadUpNextAsync(
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

        var merged = personal.Concat(team)
            .GroupBy(r => r.EventId).Select(g => g.First()) // personal/team can't collide, but stay safe
            .OrderBy(r => r.StartsAt).ThenBy(r => r.EventId)
            .Select(HomeProjections.ToItem)
            .ToList();

        // True total across both sources (mode-exclusive ⇒ no overlap; team de-duped by event).
        var personalCount = await _db.EventSignups.AsNoTracking()
            .CountAsync(s => s.UserId == userId && s.Event.Status == EventStatus.Published && s.Event.EndsAt >= now, ct);
        var teamCount = myTeamIds.Count == 0 ? 0 : await _db.EventSignups.AsNoTracking()
            .Where(s => s.TeamId != null && myTeamIds.Contains(s.TeamId!.Value)
                && s.Event.Status == EventStatus.Published && s.Event.EndsAt >= now)
            .Select(s => s.EventId).Distinct().CountAsync(ct);

        var page = merged.Skip(skip).Take(take).ToList();
        return (page, personalCount + teamCount);
    }

    /// <summary>Upcoming published individuals-mode events the viewer has not joined (RSVP prompts).</summary>
    private async Task<List<UpNextItemDto>> LoadOpenToEveryoneAsync(
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

    /// <summary>Team news (member teams) + event news (connected events), merged newest-first.</summary>
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

        var merged = HomeNewsMerge.Merge(teamNews, eventNews);
        var page = merged.Skip(skip).Take(take).ToList();
        return (page, merged.Count);
    }
}
