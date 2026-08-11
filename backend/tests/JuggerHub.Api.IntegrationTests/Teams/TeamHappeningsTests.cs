using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// The team-internal "What's happening" feed (feature 044, GH #178): members-only, derived on read,
/// windowed to 30 days and capped at 10 entries.
///
/// <para>
/// Feature 044 deliberately did <b>not</b> merge this into the team's existing event history
/// (decision D5) — that card stays signed-in-visible and untouched, and these tests assert both the
/// new behaviour and that separation.
/// </para>
/// </summary>
[Collection("Teams")]
public sealed class TeamHappeningsTests
{
    private const int MaxEntries = 10;
    private const int WindowDays = 30;

    private readonly JuggerHubApiFactory _factory;

    public TeamHappeningsTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US1: the four kinds ---------------------------------------------------

    [Fact]
    public async Task Member_join_appears_with_the_joiners_name()
    {
        var (client, viewerId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, viewerId, TeamRole.Admin);

        var (_, joinerId, joinerHandle) = await NewUserAsync();
        await AddMemberAsync(teamId, joinerId);

        var items = await GetHappeningsAsync(client, slug);

        var join = Assert.Single(items, h => h.GetProperty("kind").GetString() == "MemberJoined"
            && h.GetProperty("linkTarget").GetString() == joinerHandle);
        Assert.False(string.IsNullOrWhiteSpace(join.GetProperty("params").GetProperty("actorName").GetString()));
    }

    [Fact]
    public async Task Team_badge_and_achievement_both_appear_as_recognitions()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        await GrantTeamBadgeAsync(teamId, "Fair play", userId);
        await GrantTeamAchievementAsync(teamId, "Regional champions", userId);

        var items = await GetHappeningsAsync(client, slug);
        var names = items
            .Where(h => h.GetProperty("kind").GetString() == "RecognitionAwarded")
            .Select(h => h.GetProperty("params").GetProperty("recognitionName").GetString())
            .ToList();

        Assert.Contains("Fair play", names);
        Assert.Contains("Regional champions", names);
    }

    [Fact]
    public async Task Training_series_creation_appears_once_and_a_cancelled_session_names_its_date()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        var sessionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var (_, sessionId) = await SeedTrainingAsync(teamId, userId, "Tuesday practice", [sessionDate]);
        await CancelSessionAsync(sessionId);

        var items = await GetHappeningsAsync(client, slug);

        var created = Assert.Single(items, h => h.GetProperty("kind").GetString() == "TrainingSeriesCreated");
        Assert.Equal("Tuesday practice", created.GetProperty("params").GetProperty("trainingName").GetString());

        var cancelled = Assert.Single(items, h => h.GetProperty("kind").GetString() == "TrainingSessionCancelled");
        Assert.Equal("Tuesday practice", cancelled.GetProperty("params").GetProperty("trainingName").GetString());
        Assert.Equal(sessionDate.ToString("yyyy-MM-dd"), cancelled.GetProperty("params").GetProperty("sessionDate").GetString());
        Assert.Equal(sessionId.ToString(), cancelled.GetProperty("linkTarget").GetString());
    }

    /// <summary>
    /// THE FLOOD GUARD (feature 044 decision D3, SC-004). <c>RecurrenceExpander.MaxSessions</c> is 520
    /// and <c>TrainingSeriesService</c> materializes the whole expansion in one save, so a feed kind
    /// derived from <c>TrainingSessions.CreatedDate</c> would emit one entry per generated session —
    /// all sharing a timestamp — and bury every other kind. The series is read from
    /// <c>Trainings</c>, so a series is exactly one entry however many sessions it spawns.
    /// </summary>
    [Fact]
    public async Task A_long_recurring_series_produces_exactly_one_entry()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var weekly = Enumerable.Range(0, 104).Select(i => start.AddDays(7 * i)).ToArray();
        await SeedTrainingAsync(teamId, userId, "Weekly practice", weekly);

        var items = await GetHappeningsAsync(client, slug);

        Assert.Single(items, h => h.GetProperty("kind").GetString() == "TrainingSeriesCreated");
        Assert.DoesNotContain(items, h => h.GetProperty("kind").GetString() == "TrainingSessionCreated");
        Assert.DoesNotContain(items, h => h.GetProperty("kind").GetString() == "TrainingSessionScheduled");
    }

    // --- US1: bounds -----------------------------------------------------------

    [Fact]
    public async Task Never_returns_more_than_the_cap()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        for (var i = 0; i < MaxEntries + 5; i++)
        {
            var (_, joinerId, _) = await NewUserAsync();
            await AddMemberAsync(teamId, joinerId);
        }

        var items = await GetHappeningsAsync(client, slug);

        Assert.Equal(MaxEntries, items.Count);
    }

    [Fact]
    public async Task Happenings_older_than_the_window_are_excluded()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        var (_, recentId, recentHandle) = await NewUserAsync();
        await AddMemberAsync(teamId, recentId);

        var (_, ancientId, ancientHandle) = await NewUserAsync();
        await AddMemberAsync(teamId, ancientId, joinedAt: DateTime.UtcNow.AddDays(-(WindowDays + 10)));

        var items = await GetHappeningsAsync(client, slug);
        var handles = items.Select(h => h.GetProperty("linkTarget").GetString()).ToList();

        Assert.Contains(recentHandle, handles);
        Assert.DoesNotContain(ancientHandle, handles);
        Assert.All(items, h => Assert.True(
            h.GetProperty("occurredAt").GetDateTime() >= DateTime.UtcNow.AddDays(-WindowDays).AddMinutes(-1)));
    }

    /// <summary>
    /// Ordering must be total and repeatable (FR-015). Entries sharing a timestamp are realistic —
    /// a batch grant, or a series created and one of its sessions cancelled in the same breath.
    /// </summary>
    [Fact]
    public async Task Ordering_is_newest_first_and_repeatable()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        // Three joins sharing one instant, plus two recognitions sharing another.
        var shared = DateTime.UtcNow.AddDays(-1);
        for (var i = 0; i < 3; i++)
        {
            var (_, joinerId, _) = await NewUserAsync();
            await AddMemberAsync(teamId, joinerId, joinedAt: shared);
        }

        await GrantTeamBadgeAsync(teamId, "Alpha", userId);
        await GrantTeamBadgeAsync(teamId, "Beta", userId);

        var first = await GetHappeningsAsync(client, slug);
        var second = await GetHappeningsAsync(client, slug);

        var firstKeys = first.Select(Key).ToList();
        Assert.Equal(firstKeys, second.Select(Key).ToList());

        var times = first.Select(h => h.GetProperty("occurredAt").GetDateTime()).ToList();
        Assert.Equal(times.OrderByDescending(t => t).ToList(), times);

        static string Key(JsonElement h) =>
            $"{h.GetProperty("kind").GetString()}|{h.GetProperty("linkTarget").GetString()}|{h.GetProperty("occurredAt").GetDateTime():O}|{h.GetProperty("params").GetProperty("recognitionName").GetString()}";
    }

    // --- US1: the feed self-corrects because it is derived ---------------------

    [Fact]
    public async Task A_departed_members_join_disappears_with_them()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        var (_, leaverId, leaverHandle) = await NewUserAsync();
        await AddMemberAsync(teamId, leaverId);

        Assert.Contains(await GetHappeningsAsync(client, slug),
            h => h.GetProperty("linkTarget").GetString() == leaverHandle);

        await WithDbAsync(db => db.TeamMemberships
            .Where(m => m.TeamId == teamId && m.UserId == leaverId)
            .ExecuteDeleteAsync());

        Assert.DoesNotContain(await GetHappeningsAsync(client, slug),
            h => h.GetProperty("linkTarget").GetString() == leaverHandle);
    }

    [Fact]
    public async Task A_revoked_award_disappears()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        var awardId = await GrantTeamBadgeAsync(teamId, "Temporary honour", userId);

        Assert.Contains(await GetHappeningsAsync(client, slug),
            h => h.GetProperty("params").GetProperty("recognitionName").GetString() == "Temporary honour");

        await WithDbAsync(db => db.BadgeAwards.Where(b => b.Id == awardId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(b => b.Status, AwardStatus.Revoked)
                .SetProperty(b => b.RevokedAt, DateTime.UtcNow)
                .SetProperty(b => b.ModifiedDate, DateTime.UtcNow)));

        Assert.DoesNotContain(await GetHappeningsAsync(client, slug),
            h => h.GetProperty("params").GetProperty("recognitionName").GetString() == "Temporary honour");
    }

    /// <summary>
    /// A banned player's identity is suppressed everywhere, so the entry survives with a null name
    /// and the client substitutes a <em>translated</em> stand-in. A server-side English placeholder
    /// here would render "A former player" inside a German page.
    /// </summary>
    [Fact]
    public async Task A_banned_members_entry_survives_without_disclosing_their_name()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        var (_, bannedId, bannedHandle) = await NewUserAsync();
        await AddMemberAsync(teamId, bannedId);
        // User derives from IdentityUser, not BaseEntity — no audit columns to maintain here.
        await WithDbAsync(db => db.Users.Where(u => u.Id == bannedId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, AccountStatus.Banned)));

        var items = await GetHappeningsAsync(client, slug);
        var joins = items.Where(h => h.GetProperty("kind").GetString() == "MemberJoined").ToList();

        // The banned player's join survives as a happening — the team did gain a member — but carries
        // no identity at all: the ban query filter on PlayerProfiles suppresses the display name AND
        // the handle, so neither the sentence nor the link can name them.
        Assert.Equal(2, joins.Count); // the viewer's own join, plus the banned player's
        var anonymised = Assert.Single(joins, h => h.GetProperty("params").GetProperty("actorName").ValueKind == JsonValueKind.Null);
        Assert.Equal(JsonValueKind.Null, anonymised.GetProperty("linkTarget").ValueKind);
        Assert.DoesNotContain(bannedHandle, items.Select(i => i.ToString()).Aggregate(string.Concat), StringComparison.OrdinalIgnoreCase);
    }

    // --- US1: what the feed must never contain ---------------------------------

    /// <summary>
    /// Events belong to the separate "Recent events" card (FR-008), and departures/role changes are
    /// excluded outright (decision D1) because nothing the platform records can reconstruct them.
    /// </summary>
    [Fact]
    public async Task Feed_carries_no_event_departure_or_role_change_entries()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        var (_, otherId, _) = await NewUserAsync();
        await AddMemberAsync(teamId, otherId);

        // Promote, then demote — a role change that must leave no trace in the feed.
        await WithDbAsync(db => db.TeamMemberships.Where(m => m.TeamId == teamId && m.UserId == otherId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(m => m.Role, TeamRole.Admin)
                .SetProperty(m => m.ModifiedDate, DateTime.UtcNow)));

        var items = await GetHappeningsAsync(client, slug);

        var kinds = items.Select(h => h.GetProperty("kind").GetString()).Distinct().ToList();
        Assert.All(kinds, k => Assert.Contains(k, new[]
        {
            "MemberJoined", "RecognitionAwarded", "TrainingSeriesCreated", "TrainingSessionCancelled",
        }));
    }

    /// <summary>
    /// The regression tripwire inherited from the dashboard feed: entries carry facts, never a
    /// server-composed sentence. Prose built here would be English on a German page and invisible to
    /// the catalogue key-parity guard, because prose that never became a key cannot be missing.
    /// </summary>
    [Fact]
    public async Task Entries_carry_facts_not_a_server_rendered_sentence()
    {
        var (client, userId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, userId, TeamRole.Admin);

        var (_, joinerId, _) = await NewUserAsync();
        await AddMemberAsync(teamId, joinerId);

        var items = await GetHappeningsAsync(client, slug);

        Assert.NotEmpty(items);
        foreach (var h in items)
        {
            Assert.False(h.TryGetProperty("summary", out _));
            Assert.False(h.TryGetProperty("text", out _));
            Assert.False(h.TryGetProperty("actions", out _));
            Assert.Equal(JsonValueKind.Object, h.GetProperty("params").ValueKind);
        }
    }

    // --- US3: nothing leaks to a non-member ------------------------------------

    [Fact]
    public async Task Non_member_and_unknown_team_are_indistinguishable()
    {
        var (_, ownerId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, ownerId, TeamRole.Admin);

        var (outsider, _, _) = await NewUserAsync();

        var forReal = await outsider.GetAsync($"/api/v1/teams/{slug}/happenings");
        var forGhost = await outsider.GetAsync($"/api/v1/teams/{NewSlug()}/happenings");

        Assert.Equal(HttpStatusCode.NotFound, forReal.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, forGhost.StatusCode);

        // Bodies must be identical apart from the per-request traceId, which is not a signal about
        // the team. Anything else differing would tell an outsider the team exists.
        Assert.Equal(
            await WithoutTraceIdAsync(forGhost),
            await WithoutTraceIdAsync(forReal));

        static async Task<string> WithoutTraceIdAsync(HttpResponseMessage resp)
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return string.Join('|', doc.RootElement.EnumerateObject()
                .Where(p => p.Name != "traceId")
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{p.Name}={p.Value}"));
        }
    }

    [Fact]
    public async Task Anonymous_caller_is_unauthorized()
    {
        var (_, ownerId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, ownerId, TeamRole.Admin);

        var anonymous = _factory.CreateClient();
        var resp = await anonymous.GetAsync($"/api/v1/teams/{slug}/happenings");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    /// <summary>
    /// Trainings default to team-only visibility, so a cancelled session is exactly the sort of fact
    /// that must not reach an outsider. The whole card being members-only is what guarantees it.
    /// </summary>
    [Fact]
    public async Task A_team_only_trainings_cancellation_never_reaches_an_outsider()
    {
        var (_, ownerId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, ownerId, TeamRole.Admin);

        var (_, sessionId) = await SeedTrainingAsync(
            teamId, ownerId, "Secret drills", [DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))]);
        await CancelSessionAsync(sessionId);

        var (outsider, _, _) = await NewUserAsync();

        var direct = await outsider.GetAsync($"/api/v1/teams/{slug}/happenings");
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
        Assert.DoesNotContain("Secret drills", await direct.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // The signed-in-visible team payload must not have grown a leak either.
        var pub = await outsider.GetStringAsync($"/api/v1/teams/{slug}/public");
        Assert.DoesNotContain("Secret drills", pub, StringComparison.Ordinal);
        Assert.DoesNotContain("happening", pub, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// FR-016 / SC-002: the existing event card is frozen. Only the heading the client renders above
    /// it changed; the payload did not.
    /// </summary>
    [Fact]
    public async Task The_public_payload_still_carries_its_event_activity_array()
    {
        var (_, ownerId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, ownerId, TeamRole.Admin);

        var (outsider, _, _) = await NewUserAsync();
        var pub = await outsider.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/public");

        Assert.Equal(JsonValueKind.Array, pub.GetProperty("recentActivity").ValueKind);
    }

    // --- helpers ---------------------------------------------------------------

    private async Task<List<JsonElement>> GetHappeningsAsync(HttpClient client, string slug)
    {
        var resp = await client.GetAsync($"/api/v1/teams/{slug}/happenings");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.EnumerateArray().ToList();
    }

    private async Task<(HttpClient Client, Guid UserId, string Handle)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle);
    }

    private static string NewSlug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    private Task WithDbAsync(Func<AppDbContext, Task> action) =>
        WithDbAsync(async db => { await action(db); return true; });

    private Task<(Guid Id, string Slug)> SeedTeamAsync() =>
        WithDbAsync(async db =>
        {
            var team = new Team { Slug = NewSlug(), Name = "Rheinfeuer", Type = TeamType.Mixteam };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
            return (team.Id, team.Slug);
        });

    private Task AddMemberAsync(Guid teamId, Guid userId, TeamRole role = TeamRole.Member, DateTime? joinedAt = null) =>
        WithDbAsync(async db =>
        {
            db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = teamId,
                UserId = userId,
                Role = role,
                JoinedDate = joinedAt ?? DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    private Task<Guid> GrantTeamBadgeAsync(Guid teamId, string name, Guid grantedBy) =>
        WithDbAsync(async db =>
        {
            var def = new BadgeDefinition { Name = name, Description = "Seeded.", AppliesToTeams = true };
            var award = new BadgeAward
            {
                Definition = def,
                TeamId = teamId,
                Source = AwardSource.Manual,
                Status = AwardStatus.Active,
                EarnedAt = DateTime.UtcNow,
                GrantedByUserId = grantedBy,
            };
            db.BadgeAwards.Add(award);
            await db.SaveChangesAsync();
            return award.Id;
        });

    private Task<Guid> GrantTeamAchievementAsync(Guid teamId, string name, Guid grantedBy) =>
        WithDbAsync(async db =>
        {
            var def = new AchievementDefinition { Name = name, Description = "Seeded.", AppliesToTeams = true };
            var award = new AchievementAward
            {
                Definition = def,
                TeamId = teamId,
                Source = AwardSource.Manual,
                Status = AwardStatus.Active,
                EarnedAt = DateTime.UtcNow,
                GrantedByUserId = grantedBy,
            };
            db.AchievementAwards.Add(award);
            await db.SaveChangesAsync();
            return award.Id;
        });

    /// <summary>Seeds a training series plus its sessions; returns the series id and the first session id.</summary>
    private Task<(Guid TrainingId, Guid FirstSessionId)> SeedTrainingAsync(
        Guid teamId, Guid createdBy, string name, IReadOnlyList<DateOnly> sessionDates) =>
        WithDbAsync(async db =>
        {
            var training = new Training
            {
                TeamId = teamId,
                Name = name,
                LocationKind = LocationKind.Virtual,
                VirtualLink = "https://example.com/meet",
                IsRecurring = sessionDates.Count > 1,
                Weekday = sessionDates.Count > 1 ? sessionDates[0].DayOfWeek : null,
                Interval = sessionDates.Count > 1 ? TrainingInterval.Weekly : null,
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(20, 0),
                StartDate = sessionDates[0],
                EndDate = sessionDates[^1],
                Visibility = TrainingVisibility.TeamOnly,
                CreatedByUserId = createdBy,
            };
            db.Trainings.Add(training);

            foreach (var d in sessionDates)
            {
                training.Sessions.Add(new TrainingSession
                {
                    Training = training,
                    TeamId = teamId,
                    SessionDate = d,
                    Status = TrainingSessionStatus.Scheduled,
                });
            }

            await db.SaveChangesAsync();
            return (training.Id, training.Sessions.OrderBy(s => s.SessionDate).First().Id);
        });

    private Task CancelSessionAsync(Guid sessionId) =>
        WithDbAsync(db => db.TrainingSessions.Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, TrainingSessionStatus.Cancelled)
                .SetProperty(s => s.CancelledDate, DateTime.UtcNow)
                .SetProperty(s => s.ModifiedDate, DateTime.UtcNow)));
}
