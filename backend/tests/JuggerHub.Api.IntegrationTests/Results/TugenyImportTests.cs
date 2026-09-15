using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>
/// Importing a finalized Tugeny tournament (feature 050, US4). Runs the shipped client, pipeline and
/// size guard against the committed real responses for the 25th German championship: 20 teams,
/// 78 matches, 9 of them draws (SC-002).
/// </summary>
[Collection("Results")]
public sealed class TugenyImportTests : ResultsTestSupport
{
    private const string Address = "25-deutsche-meisterschaft";
    private const int SevenSins = 40; // Tugeny's team id for the champions

    public TugenyImportTests(JuggerHubApiFactory factory) : base(factory)
    {
    }

    /// <summary>A started, linked tournament on a host talking to <paramref name="stub"/>, and its admin.</summary>
    private async Task<(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host, HttpClient Admin, Guid EventId)> LinkedAsync(TugenyStub stub)
    {
        var (_, _, _, email) = await NewUserAsync();
        var host = HostWith(stub);
        var admin = await SignInAsync(host, email);
        var eventId = await CreateTournamentAsync(admin);
        await ReadJsonAsync(await admin.PutAsJsonAsync($"{EventResults(eventId)}/tugeny-link", new { address = Address }));
        await StartEventAsync(eventId);
        return (host, admin, eventId);
    }

    private static Task<HttpResponseMessage> CommitAsync(HttpClient admin, Guid eventId, params (int TugenyTeamId, Guid TeamId)[] connections) =>
        admin.PostAsJsonAsync($"{EventResults(eventId)}/tugeny-import", new
        {
            connections = connections.Select(c => new { tugenyTeamId = c.TugenyTeamId, teamId = c.TeamId }),
        });

    [Fact]
    public async Task The_preview_is_a_draft_and_saves_nothing()
    {
        var (host, admin, eventId) = await LinkedAsync(TugenyStub.Finalized());
        using var _ = host;

        var preview = await ReadJsonAsync(await admin.GetAsync($"{EventResults(eventId)}/tugeny-import"));

        Assert.Equal(20, preview.GetProperty("placements").GetArrayLength());
        Assert.Equal(78, preview.GetProperty("matchCount").GetInt32());
        Assert.Equal("Seven Sins", preview.GetProperty("placements")[0].GetProperty("name").GetString());
        Assert.False(preview.GetProperty("replacesExisting").GetBoolean());
        Assert.False(await WithDbAsync(db => db.TournamentPlacements.AnyAsync(p => p.TournamentResult.EventId == eventId)));
    }

    [Fact]
    public async Task A_tournament_not_finalized_in_tugeny_cannot_be_imported()
    {
        var (host, admin, eventId) = await LinkedAsync(TugenyStub.NotFinalized());
        using var _ = host;

        var response = await admin.GetAsync($"{EventResults(eventId)}/tugeny-import");

        await AssertStatusAsync(response, HttpStatusCode.UnprocessableEntity);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.EndsWith("not-finalized", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Committing_refetches_and_stores_the_ranking_and_every_match()
    {
        var stub = TugenyStub.Finalized();
        var (host, admin, eventId) = await LinkedAsync(stub);
        using var _ = host;
        var (teamId, slug) = await NewTeamAsync();
        await SeedSignupAsync(eventId, teamId);
        var before = stub.Calls;

        var result = await ReadJsonAsync(await CommitAsync(admin, eventId, (SevenSins, teamId)));

        Assert.Equal(2, stub.Calls - before); // ranking + matches, fetched again — provenance is the server's
        Assert.Equal("TugenyImport", result.GetProperty("source").GetString());
        Assert.Equal(20, result.GetProperty("rankedCount").GetInt32());
        Assert.Equal(78, result.GetProperty("matchCount").GetInt32());
        var first = result.GetProperty("placements")[0];
        Assert.Equal(1, first.GetProperty("position").GetInt32());
        Assert.Equal(slug, first.GetProperty("teamSlug").GetString());

        var matches = await WithDbAsync(db => db.TournamentMatches
            .Where(m => m.TournamentResult.EventId == eventId).OrderBy(m => m.SortIndex).ToListAsync());
        Assert.Equal(78, matches.Count);
        Assert.Equal(9, matches.Count(m => m.Winner == MatchWinner.Draw));
        Assert.Equal(
            ["Group 1", "Group 2", "Group 3", "Group 4", "Group 5"],
            matches.Where(m => m.Stage != null).Select(m => m.Stage!).Distinct().Order().ToArray());
        Assert.Contains(matches, m => m.Stage == null);

        var final = matches.Single(m => m.Name == "F 1-2");
        Assert.Equal([5, 5, 2, 5], final.FirstScores);
        Assert.Equal([4, 2, 5, 4], final.SecondScores);
        Assert.Equal(MatchWinner.First, final.Winner);
        Assert.NotNull(final.FirstPlacementId);

        // Tugeny's list is not in time order; the stored order follows its timestamps.
        var fixture = JsonDocument.Parse(Fixture("matches.finalized.json")).RootElement.EnumerateArray()
            .Select(m => (Name: m.GetProperty("name").GetString()!, Time: m.GetProperty("timestamp").GetString()!))
            .ToList();
        var times = matches.Select(m => fixture.First(f => f.Name == m.Name).Time).ToList();
        Assert.Equal(times.Order(StringComparer.Ordinal), times);
    }

    [Fact]
    public async Task A_connection_to_a_team_without_a_confirmed_signup_is_refused()
    {
        var (host, admin, eventId) = await LinkedAsync(TugenyStub.Finalized());
        using var _ = host;
        var (pending, _) = await NewTeamAsync();
        await SeedSignupAsync(eventId, pending, SignupStatus.AwaitingApproval);

        await AssertStatusAsync(await CommitAsync(admin, eventId, (SevenSins, pending)), HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Two_tugeny_teams_cannot_become_one_juggerhub_team()
    {
        var (host, admin, eventId) = await LinkedAsync(TugenyStub.Finalized());
        using var _ = host;
        var (teamId, _) = await NewTeamAsync();
        await SeedSignupAsync(eventId, teamId);

        await AssertStatusAsync(await CommitAsync(admin, eventId, (SevenSins, teamId), (42, teamId)), HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_failed_fetch_leaves_the_saved_ranking_exactly_as_it_was()
    {
        var stub = new TugenyStub()
            .Serve("/api/persistent/tournamentsBySlug/", Fixture("tournament-by-slug.finalized.json"))
            .Serve("/api/persistent/rankingsByTournamentId/", "{}", HttpStatusCode.InternalServerError);
        var (host, admin, eventId) = await LinkedAsync(stub);
        using var _ = host;
        await ReadJsonAsync(await SaveRankingAsync(admin, eventId, (null, 1, "Alpha", null), (null, 2, "Bravo", null)));

        await AssertStatusAsync(await CommitAsync(admin, eventId), HttpStatusCode.ServiceUnavailable);

        var names = await WithDbAsync(db => db.TournamentPlacements
            .Where(p => p.TournamentResult.EventId == eventId).OrderBy(p => p.Position).Select(p => p.Name).ToListAsync());
        Assert.Equal(["Alpha", "Bravo"], names);
    }

    [Fact]
    public async Task An_oversized_body_fails_once_without_a_retry()
    {
        var stub = new TugenyStub()
            .Serve("/api/persistent/tournamentsBySlug/", Fixture("tournament-by-slug.finalized.json"))
            .ServeBytes("/api/persistent/rankingsByTournamentId/", (4 * 1024 * 1024) + 1);
        var (host, admin, eventId) = await LinkedAsync(stub);
        using var _ = host;
        var before = stub.Calls;

        await AssertStatusAsync(await admin.GetAsync($"{EventResults(eventId)}/tugeny-import"), HttpStatusCode.ServiceUnavailable);

        Assert.Equal(1, stub.Calls - before);
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_unusable()
    {
        var stub = new TugenyStub()
            .Serve("/api/persistent/tournamentsBySlug/", Fixture("tournament-by-slug.finalized.json"))
            .Serve("/api/persistent/rankingsByTournamentId/", "<html>maintenance</html>");
        var (host, admin, eventId) = await LinkedAsync(stub);
        using var _ = host;

        await AssertStatusAsync(await admin.GetAsync($"{EventResults(eventId)}/tugeny-import"), HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task A_hand_edit_after_the_import_is_marked_as_edited()
    {
        var (host, admin, eventId) = await LinkedAsync(TugenyStub.Finalized());
        using var _ = host;
        await ReadJsonAsync(await CommitAsync(admin, eventId));

        await ReadJsonAsync(await SaveRankingAsync(admin, eventId, (null, 1, "Only one left", null)));

        var result = await ReadJsonAsync(await admin.GetAsync(EventResults(eventId)));
        Assert.Equal("TugenyImport", result.GetProperty("source").GetString());
        Assert.True(result.GetProperty("editedSinceImport").GetBoolean());
    }

    [Fact]
    public async Task Matches_are_paged()
    {
        var (host, admin, eventId) = await LinkedAsync(TugenyStub.Finalized());
        using var _ = host;
        await ReadJsonAsync(await CommitAsync(admin, eventId));

        var page = await ReadJsonAsync(await admin.GetAsync($"{EventResults(eventId)}/matches?skip=50&take=50"));

        Assert.Equal(78, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(28, page.GetProperty("items").GetArrayLength());
    }
}
