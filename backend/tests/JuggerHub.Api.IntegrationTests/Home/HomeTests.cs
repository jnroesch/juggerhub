using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Home;

/// <summary>
/// Integration tests for the Home dashboard (feature 008): composite shape + variant, the Up-next
/// union and its exclusions, news aggregation, the entitlement invariants, and me/teams. Runs the
/// real app against a Testcontainers Postgres.
/// </summary>
[Collection("Home")]
public sealed class HomeTests
{
    private static readonly DateTime Soon = DateTime.UtcNow.AddDays(3);

    private readonly JuggerHubApiFactory _factory;

    public HomeTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Home_requires_authentication()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/api/v1/home");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Home_greets_viewer_and_lists_their_teams()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, slug) = await HomeTestSupport.SeedTeamAsync(_factory, "Bloodhounds");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId, TeamRole.Admin);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");

        Assert.False(string.IsNullOrWhiteSpace(home.GetProperty("viewer").GetProperty("displayName").GetString()));
        var teams = home.GetProperty("teams").EnumerateArray().ToList();
        Assert.Contains(teams, t => t.GetProperty("slug").GetString() == slug && t.GetProperty("role").GetString() == "Admin");
    }

    [Fact]
    public async Task UpNext_includes_personal_signup_with_toggle_and_excludes_past_and_cancelled()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);

        var future = await HomeTestSupport.SeedEventAsync(_factory, "Open training", Soon, Soon.AddHours(2), ParticipantMode.Individuals);
        var signupId = await HomeTestSupport.SignupUserAsync(_factory, future, userId);

        var past = await HomeTestSupport.SeedEventAsync(_factory, "Last week", DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(-5).AddHours(2), ParticipantMode.Individuals);
        await HomeTestSupport.SignupUserAsync(_factory, past, userId);

        var cancelled = await HomeTestSupport.SeedEventAsync(_factory, "Called off", Soon, Soon.AddHours(2), ParticipantMode.Individuals, status: EventStatus.Cancelled);
        await HomeTestSupport.SignupUserAsync(_factory, cancelled, userId);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var titles = home.GetProperty("upNext").EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();

        Assert.Contains("Open training", titles);
        Assert.DoesNotContain("Last week", titles);
        Assert.DoesNotContain("Called off", titles);

        var item = home.GetProperty("upNext").EnumerateArray().First(i => i.GetProperty("title").GetString() == "Open training");
        Assert.Equal("Individuals", item.GetProperty("mode").GetString());
        Assert.Equal(signupId.ToString(), item.GetProperty("viewerSignupId").GetString());
        Assert.Equal("Joined", item.GetProperty("viewerStatus").GetString());
        Assert.True(item.GetProperty("teamGoing").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task UpNext_includes_teams_entry_as_read_only_team_going()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, slug) = await HomeTestSupport.SeedTeamAsync(_factory, "Rooks");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);

        var match = await HomeTestSupport.SeedEventAsync(_factory, "League match", Soon, Soon.AddHours(2), ParticipantMode.Teams);
        await HomeTestSupport.SignupTeamAsync(_factory, match, teamId);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var item = home.GetProperty("upNext").EnumerateArray().First(i => i.GetProperty("title").GetString() == "League match");

        Assert.Equal("Teams", item.GetProperty("mode").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("viewerSignupId").ValueKind);
        Assert.Equal(slug, item.GetProperty("teamGoing").GetProperty("slug").GetString());
    }

    [Fact]
    public async Task UpNext_does_not_leak_another_players_private_signups()
    {
        var (aClient, aUser) = await HomeTestSupport.NewUserAsync(_factory);
        var (bClient, _) = await HomeTestSupport.NewUserAsync(_factory);

        var ev = await HomeTestSupport.SeedEventAsync(_factory, "Private to A", Soon, Soon.AddHours(2), ParticipantMode.Individuals);
        await HomeTestSupport.SignupUserAsync(_factory, ev, aUser);

        var aHome = await aClient.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var bHome = await bClient.GetFromJsonAsync<JsonElement>("/api/v1/home");

        Assert.Contains(aHome.GetProperty("upNext").EnumerateArray(), i => i.GetProperty("title").GetString() == "Private to A");
        Assert.DoesNotContain(bHome.GetProperty("upNext").EnumerateArray(), i => i.GetProperty("title").GetString() == "Private to A");

        // And the paginated "see all" enforces the same boundary.
        var bUpNext = await bClient.GetFromJsonAsync<JsonElement>("/api/v1/home/up-next");
        Assert.DoesNotContain(bUpNext.GetProperty("items").EnumerateArray(), i => i.GetProperty("title").GetString() == "Private to A");
    }

    [Fact]
    public async Task News_aggregates_team_and_event_sources_tagged_and_excludes_non_member_teams()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (myTeam, mySlug) = await HomeTestSupport.SeedTeamAsync(_factory, "My team");
        await HomeTestSupport.AddMemberAsync(_factory, myTeam, userId, TeamRole.Admin);
        await HomeTestSupport.AddTeamNewsAsync(_factory, myTeam, userId, "Gear arrived");

        // An event the viewer is connected to (personal signup) with news.
        var ev = await HomeTestSupport.SeedEventAsync(_factory, "Connected", Soon, Soon.AddHours(2), ParticipantMode.Individuals);
        await HomeTestSupport.SignupUserAsync(_factory, ev, userId);
        await HomeTestSupport.AddEventNewsAsync(_factory, ev, userId, "Schedule posted");

        // A team the viewer is NOT a member of, with news — must never appear.
        var (otherTeam, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Not mine");
        await HomeTestSupport.AddTeamNewsAsync(_factory, otherTeam, userId, "Secret roster news");

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var news = home.GetProperty("news").EnumerateArray().ToList();

        Assert.Contains(news, n => n.GetProperty("source").GetString() == "team" && n.GetProperty("body").GetString() == "Gear arrived");
        Assert.Contains(news, n => n.GetProperty("source").GetString() == "event" && n.GetProperty("body").GetString() == "Schedule posted");
        Assert.DoesNotContain(news, n => n.GetProperty("body").GetString() == "Secret roster news");
        Assert.Equal(mySlug, news.First(n => n.GetProperty("source").GetString() == "team").GetProperty("sourceSlugOrId").GetString());
    }

    [Fact]
    public async Task NewPlayer_variant_has_no_teams_but_open_events_to_join()
    {
        var (client, _) = await HomeTestSupport.NewUserAsync(_factory);

        await HomeTestSupport.SeedEventAsync(_factory, "Everyone welcome", Soon, Soon.AddHours(2), ParticipantMode.Individuals);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");

        Assert.Empty(home.GetProperty("teams").EnumerateArray());
        Assert.Empty(home.GetProperty("upNext").EnumerateArray());
        Assert.Empty(home.GetProperty("needsYou").EnumerateArray());
        Assert.Contains(home.GetProperty("openToEveryone").EnumerateArray(), i => i.GetProperty("title").GetString() == "Everyone welcome");
        var item = home.GetProperty("openToEveryone").EnumerateArray().First(i => i.GetProperty("title").GetString() == "Everyone welcome");
        Assert.Equal("Event", item.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task MyTeams_returns_only_the_callers_memberships()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (mine, mineSlug) = await HomeTestSupport.SeedTeamAsync(_factory, "Mine");
        await HomeTestSupport.AddMemberAsync(_factory, mine, userId, TeamRole.Admin);
        var (theirs, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Theirs"); // not joined

        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me/teams");
        var slugs = page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("slug").GetString()).ToList();

        Assert.Single(slugs, s => s == mineSlug);
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task MyTeams_requires_authentication()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/api/v1/profiles/me/teams");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
