using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Home;

/// <summary>
/// Integration tests for the no-team (new-player) variant under the reshaped contract (feature 025,
/// US5): open-to-everyone is populated as agenda items while every team/party-scoped section stays
/// empty, so the client renders the warm find-a-team experience without regression.
/// </summary>
[Collection("Home")]
public sealed class NoTeamVariantTests
{
    private static readonly DateTime Soon = DateTime.UtcNow.AddDays(3);

    private readonly JuggerHubApiFactory _factory;

    public NoTeamVariantTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task No_team_viewer_gets_open_events_and_empty_team_scoped_sections()
    {
        var (client, _) = await HomeTestSupport.NewUserAsync(_factory);

        await HomeTestSupport.SeedEventAsync(_factory, "Everyone welcome", Soon, Soon.AddHours(2), ParticipantMode.Individuals);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");

        Assert.Empty(home.GetProperty("teams").EnumerateArray());
        Assert.Empty(home.GetProperty("needsYou").EnumerateArray());
        Assert.Empty(home.GetProperty("upNext").EnumerateArray());
        Assert.Empty(home.GetProperty("news").EnumerateArray());
        Assert.Empty(home.GetProperty("activity").EnumerateArray());

        var open = home.GetProperty("openToEveryone").EnumerateArray().ToList();
        var item = open.First(i => i.GetProperty("title").GetString() == "Everyone welcome");
        Assert.Equal("Event", item.GetProperty("kind").GetString());
        // An open RSVP prompt: individuals-mode, no viewer sign-up yet.
        Assert.Equal("Individuals", item.GetProperty("mode").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("viewerSignupId").ValueKind);
    }
}
