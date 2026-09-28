using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Teams;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>
/// Feature 061, FR-010: a connected placement shows its team's CURRENT name — the rule feature 050
/// wrote down ("a team is renamed later: its placements show its current name") and could not break
/// while nothing renamed a team. A rename refreshes the team's connected placements and, through
/// them, every match side linked to one; it is not a change to anyone's results.
/// </summary>
[Collection("Results")]
public sealed class TeamRenamePlacementTests : ResultsTestSupport
{
    public TeamRenamePlacementTests(JuggerHubApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task A_renamed_teams_placements_and_matches_show_the_new_name()
    {
        var s = await ArrangeAsync();
        var before = await ResultAsync(s.EventId);

        await RenameAsync(s, "Rheinfeuer Köln");

        var ranking = await ResultAsync(s.EventId);
        var ours = ranking.Placements.Single(p => p.TeamSlug == s.Slug);
        Assert.Equal("Rheinfeuer Köln", ours.Name);
        // A rename is not a change to the results (050 shows this date to readers).
        Assert.Equal(before.ResultsChangedAt, ranking.ResultsChangedAt);

        var matches = await ReadJsonAsync(await s.Admin.GetAsync($"{EventResults(s.EventId)}/matches"));
        var match = matches.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("Rheinfeuer Köln", match.GetProperty("first").GetProperty("name").GetString());
        Assert.Equal("Guests", match.GetProperty("second").GetProperty("name").GetString());

        // What the organiser typed is kept as it was (050 R9).
        Assert.Equal("ours", await SourceNameAsync(ours.Id));
    }

    [Fact]
    public async Task Placements_not_connected_to_the_team_do_not_change()
    {
        var s = await ArrangeAsync();

        await RenameAsync(s, "Rheinfeuer Köln");

        var ranking = await ResultAsync(s.EventId);
        // A plain name that happens to be the team's old name, and another team's placement.
        Assert.Equal("Rheinfeuer", ranking.Placements.Single(p => p.TeamSlug is null && p.Name == "Rheinfeuer").Name);
        Assert.Equal("Rheinfeuer", ranking.Placements.Single(p => p.TeamSlug == s.OtherSlug).Name);
    }

    [Fact]
    public async Task Saving_with_the_same_name_touches_no_placement()
    {
        var s = await ArrangeAsync();
        var before = await PlacementModifiedDatesAsync(s.EventId);

        (await s.Admin.PutAsJsonAsync($"/api/v1/teams/{s.Slug}/details",
            TeamDetailsTests.Details("Rheinfeuer", type: "Mixteam", city: null, description: "Neu.")))
            .EnsureSuccessStatusCode();

        Assert.Equal(before, await PlacementModifiedDatesAsync(s.EventId));
    }

    // --- arrange / helpers ---------------------------------------------------------------------------

    private sealed record Setup(HttpClient Admin, Guid EventId, string Slug, string OtherSlug);

    private sealed record Placement(Guid Id, string Name, string? TeamSlug);

    private sealed record Ranking(DateTime? ResultsChangedAt, List<Placement> Placements);

    /// <summary>
    /// A past tournament ranking: our team (Mixteam "Rheinfeuer") connected as "ours", a plain guest
    /// entry whose name equals our old name, a second team "Rheinfeuer" connected, and "Guests" —
    /// plus one match between our placement and the guests, linked the way an import links it.
    /// </summary>
    private async Task<Setup> ArrangeAsync()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(admin);
        var (otherTeamId, otherSlug) = await NewTeamAsync();

        var eventId = await CreateTournamentAsync(admin);
        var start = DateTime.UtcNow.AddMonths(-1);
        await SetEventDatesAsync(eventId, start, start.AddDays(1));
        await SeedSignupAsync(eventId, teamId);
        await SeedSignupAsync(eventId, otherTeamId);

        await ReadJsonAsync(await SaveRankingAsync(admin, eventId,
            (null, 1, "ours", teamId),
            (null, 2, "Rheinfeuer", otherTeamId),
            (null, 3, "Rheinfeuer", null),
            (null, 4, "Guests", null)));

        await WithDbAsync(async db =>
        {
            var placements = await db.TournamentPlacements.Where(p => p.TournamentResult.EventId == eventId).ToListAsync();
            var ours = placements.Single(p => p.TeamId == teamId);
            var guests = placements.Single(p => p.SourceName == "Guests");
            db.TournamentMatches.Add(new TournamentMatch
            {
                TournamentResultId = ours.TournamentResultId,
                SortIndex = 0,
                Name = "F 1-2",
                FirstName = "ours",
                SecondName = "Guests",
                FirstPlacementId = ours.Id,
                SecondPlacementId = guests.Id,
                FirstScores = [5],
                SecondScores = [3],
                Winner = MatchWinner.First,
            });
            await db.SaveChangesAsync();
        });

        return new Setup(admin, eventId, slug, otherSlug);
    }

    private static async Task RenameAsync(Setup s, string name) =>
        (await s.Admin.PutAsJsonAsync($"/api/v1/teams/{s.Slug}/details",
            TeamDetailsTests.Details(name, type: "Mixteam", city: null))).EnsureSuccessStatusCode();

    private async Task<Ranking> ResultAsync(Guid eventId)
    {
        var result = await ReadJsonAsync(await (await NewUserAsync()).Client.GetAsync(EventResults(eventId)));
        var changed = result.GetProperty("resultsChangedAt");
        return new Ranking(
            changed.ValueKind == System.Text.Json.JsonValueKind.Null ? null : changed.GetDateTime(),
            result.GetProperty("placements").EnumerateArray()
                .Select(p => new Placement(
                    p.GetProperty("id").GetGuid(),
                    p.GetProperty("name").GetString()!,
                    p.GetProperty("teamSlug").GetString()))
                .ToList());
    }

    private Task<string> SourceNameAsync(Guid placementId) =>
        WithDbAsync(db => db.TournamentPlacements.AsNoTracking()
            .Where(p => p.Id == placementId).Select(p => p.SourceName).SingleAsync());

    private Task<Dictionary<Guid, DateTime>> PlacementModifiedDatesAsync(Guid eventId) =>
        WithDbAsync(db => db.TournamentPlacements.AsNoTracking()
            .Where(p => p.TournamentResult.EventId == eventId)
            .ToDictionaryAsync(p => p.Id, p => p.ModifiedDate));
}
