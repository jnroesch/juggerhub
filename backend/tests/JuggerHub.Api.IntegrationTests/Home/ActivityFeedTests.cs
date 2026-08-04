using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Home;

/// <summary>
/// Integration tests for the "What's going on" activity feed (feature 025, US4): passive
/// participation/social signals derived on read, scoped to the viewer's teams, read-only, and
/// disjoint from the authored News stream.
/// </summary>
[Collection("Home")]
public sealed class ActivityFeedTests
{
    private static readonly DateTime Soon = DateTime.UtcNow.AddDays(3);

    private readonly JuggerHubApiFactory _factory;

    public ActivityFeedTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Teammate_signups_and_new_members_appear_as_read_only_activity()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Ironsides");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);

        // A teammate joins the same team, then signs up for an individuals event.
        var (_, teammateId) = await HomeTestSupport.NewUserAsync(_factory);
        await HomeTestSupport.AddMemberAsync(_factory, teamId, teammateId);
        var ev = await HomeTestSupport.SeedEventAsync(_factory, "Open scrim", Soon, Soon.AddHours(2), ParticipantMode.Individuals);
        await HomeTestSupport.SignupUserAsync(_factory, ev, teammateId);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var activity = home.GetProperty("activity").EnumerateArray().ToList();

        Assert.Contains(activity, a => a.GetProperty("kind").GetString() == "TeammateJoinedEvent"
            && a.GetProperty("params").GetProperty("eventName").GetString() == "Open scrim");
        Assert.Contains(activity, a => a.GetProperty("kind").GetString() == "NewTeamMember"
            && a.GetProperty("params").GetProperty("teamName").GetString() == "Ironsides");

        // Read-only: entries carry no action fields (only kind/params/linkTarget/occurredAt).
        foreach (var a in activity)
        {
            Assert.False(a.TryGetProperty("id", out _));
            Assert.False(a.TryGetProperty("actions", out _));
        }
    }

    /// <summary>
    /// The regression tripwire for the localization defect: the feed used to ship a server-composed
    /// English sentence (<c>summary</c>), which rendered untranslated inside an otherwise German
    /// dashboard and could never be caught by the catalogue key-parity guard — prose that never
    /// became a key cannot be missing from <c>de.json</c>. Entries carry facts; the client composes
    /// the sentence from <c>home.activity.*</c>. Re-adding a rendered field here reintroduces that.
    /// </summary>
    [Fact]
    public async Task Entries_carry_facts_not_a_server_rendered_sentence()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Ravens");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);

        var (_, teammateId) = await HomeTestSupport.NewUserAsync(_factory);
        await HomeTestSupport.AddMemberAsync(_factory, teamId, teammateId);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var activity = home.GetProperty("activity").EnumerateArray().ToList();

        Assert.NotEmpty(activity);
        foreach (var a in activity)
        {
            Assert.False(a.TryGetProperty("summary", out _));
            Assert.Equal(JsonValueKind.Object, a.GetProperty("params").ValueKind);
        }
    }

    [Fact]
    public async Task Activity_and_news_are_disjoint_streams()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Wolves");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);

        // An authored team news post must appear only in News, never in activity.
        await HomeTestSupport.AddTeamNewsAsync(_factory, teamId, userId, "Kit order closes Friday");

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");

        Assert.Contains(home.GetProperty("news").EnumerateArray(),
            n => n.GetProperty("body").GetString() == "Kit order closes Friday");
        Assert.DoesNotContain(home.GetProperty("activity").EnumerateArray(),
            a => a.GetProperty("params").ToString().Contains("Kit order closes Friday"));
    }
}
