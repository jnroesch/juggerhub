using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>A team's placement history on its page (feature 050, US5 / FR-021, FR-022).</summary>
[Collection("Results")]
public sealed class TeamPlacementHistoryTests : ResultsTestSupport
{
    public TeamPlacementHistoryTests(JuggerHubApiFactory factory) : base(factory)
    {
    }

    /// <summary>A started tournament where <paramref name="teamId"/> is signed up and placed.</summary>
    private async Task<Guid> PlacedAsync(HttpClient admin, Guid teamId, int position, DateTime startsAt, int others = 2)
    {
        var eventId = await CreateTournamentAsync(admin);
        await SetEventDatesAsync(eventId, startsAt, startsAt.AddDays(1));
        await SeedSignupAsync(eventId, teamId);

        var rows = new List<(Guid?, int, string, Guid?)> { (null, position, "ours", teamId) };
        for (var i = 0; i < others; i++)
        {
            rows.Add((null, position + i + 1, $"Guests {i}", null));
        }

        await ReadJsonAsync(await SaveRankingAsync(admin, eventId, [.. rows]));
        return eventId;
    }

    [Fact]
    public async Task Connected_placements_are_listed_newest_first_with_the_ranking_size()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, slug) = await NewTeamAsync();
        var older = await PlacedAsync(admin, teamId, 3, DateTime.UtcNow.AddYears(-1), others: 5);
        var newer = await PlacedAsync(admin, teamId, 1, DateTime.UtcNow.AddMonths(-1), others: 1);
        var (reader, _, _, _) = await NewUserAsync();

        var page = await ReadJsonAsync(await reader.GetAsync($"/api/v1/teams/{slug}/placements"));

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(newer, items[0].GetProperty("eventId").GetGuid());
        Assert.Equal(1, items[0].GetProperty("position").GetInt32());
        Assert.Equal(2, items[0].GetProperty("rankedCount").GetInt32());
        Assert.Equal(older, items[1].GetProperty("eventId").GetGuid());
        Assert.Equal(6, items[1].GetProperty("rankedCount").GetInt32());
    }

    [Fact]
    public async Task The_history_is_paged()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, slug) = await NewTeamAsync();
        for (var i = 0; i < 3; i++)
        {
            await PlacedAsync(admin, teamId, 1, DateTime.UtcNow.AddDays(-10 - i), others: 0);
        }

        var page = await ReadJsonAsync(await admin.GetAsync($"/api/v1/teams/{slug}/placements?skip=2&take=2"));

        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task A_plain_name_is_nobodys_history()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (_, slug) = await NewTeamAsync();
        var eventId = await CreateTournamentAsync(admin);
        await StartEventAsync(eventId);
        // The team's exact name, typed — still a plain name until someone allowed connects it.
        await ReadJsonAsync(await SaveRankingAsync(admin, eventId, (null, 1, "Rheinfeuer", null)));

        var page = await ReadJsonAsync(await admin.GetAsync($"/api/v1/teams/{slug}/placements"));

        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task An_unknown_team_is_not_found_and_signed_out_callers_are_refused()
    {
        var (reader, _, _, _) = await NewUserAsync();

        await AssertStatusAsync(await reader.GetAsync("/api/v1/teams/no-such-team-050/placements"), HttpStatusCode.NotFound);
        await AssertStatusAsync(await Factory.CreateClient().GetAsync("/api/v1/teams/no-such-team-050/placements"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deleting_the_team_keeps_the_result_readable_under_its_name()
    {
        var (owner, _, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(owner);
        var (admin, _, _, _) = await NewUserAsync();
        var eventId = await PlacedAsync(admin, teamId, 1, DateTime.UtcNow.AddDays(-3), others: 1);

        await AssertStatusAsync(await owner.DeleteAsync($"/api/v1/teams/{slug}"), HttpStatusCode.NoContent);

        var result = await ReadJsonAsync(await admin.GetAsync(EventResults(eventId)));
        var first = result.GetProperty("placements")[0];
        Assert.Equal("Rheinfeuer", first.GetProperty("name").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, first.GetProperty("teamSlug").ValueKind);
        Assert.False(await WithDbAsync(db => db.TournamentPlacements.AnyAsync(p => p.TeamId == teamId)));
    }
}
