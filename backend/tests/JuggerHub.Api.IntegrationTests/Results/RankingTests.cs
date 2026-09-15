using System.Net;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>
/// Recording a tournament's final ranking by hand (feature 050, US1): who may, when, the
/// signed-up-team rule, ties, and what everyone else reads.
/// </summary>
[Collection("Results")]
public sealed class RankingTests : ResultsTestSupport
{
    public RankingTests(JuggerHubApiFactory factory) : base(factory)
    {
    }

    private async Task<(HttpClient Admin, Guid EventId)> StartedTournamentAsync(string mode = "Teams")
    {
        var (admin, _, _, _) = await NewUserAsync();
        var eventId = await CreateTournamentAsync(admin, mode);
        await StartEventAsync(eventId);
        return (admin, eventId);
    }

    // --- FR-001: when results may be recorded ---------------------------------------------

    [Fact]
    public async Task A_tournament_that_has_not_started_refuses_a_ranking()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var eventId = await CreateTournamentAsync(admin);

        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, (null, 1, "Kiel Guests", null)), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_cancelled_tournament_refuses_a_ranking()
    {
        var (admin, eventId) = await StartedTournamentAsync();
        await CancelEventAsync(admin, eventId);

        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, (null, 1, "Kiel Guests", null)), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_event_that_is_not_a_tournament_refuses_a_ranking()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var eventId = await CreateTournamentAsync(admin, type: "Workshop");
        await StartEventAsync(eventId);

        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, (null, 1, "Kiel Guests", null)), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Only_an_admin_of_the_event_may_record_it()
    {
        var (_, eventId) = await StartedTournamentAsync();
        var (stranger, _, _, _) = await NewUserAsync();

        await AssertStatusAsync(await SaveRankingAsync(stranger, eventId, (null, 1, "Kiel Guests", null)), HttpStatusCode.Forbidden);
        await AssertStatusAsync(await stranger.GetAsync($"{EventResults(eventId)}/editor"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_unknown_event_is_not_found()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var missing = Guid.NewGuid();

        await AssertStatusAsync(await admin.GetAsync(EventResults(missing)), HttpStatusCode.NotFound);
        await AssertStatusAsync(await SaveRankingAsync(admin, missing, (null, 1, "Kiel Guests", null)), HttpStatusCode.NotFound);
    }

    // --- Validation ------------------------------------------------------------------------

    [Fact]
    public async Task Invalid_rows_are_refused()
    {
        var (admin, eventId) = await StartedTournamentAsync();
        var (teamId, _) = await NewTeamAsync();
        await SeedSignupAsync(eventId, teamId);

        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, (null, 1, "   ", null)), HttpStatusCode.BadRequest);
        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, (null, 1, new string('x', 81), null)), HttpStatusCode.BadRequest);
        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, (null, 0, "Kiel Guests", null)), HttpStatusCode.BadRequest);
        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, (null, 1000, "Kiel Guests", null)), HttpStatusCode.BadRequest);
        await AssertStatusAsync(
            await SaveRankingAsync(admin, eventId, (null, 1, "A", teamId), (null, 2, "B", teamId)),
            HttpStatusCode.BadRequest);

        var tooMany = Enumerable.Range(1, 129).Select(i => ((Guid?)null, i, $"Team {i}", (Guid?)null)).ToArray();
        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, tooMany), HttpStatusCode.BadRequest);
    }

    // --- FR-004a: an event admin connects only signed-up teams ------------------------------

    [Theory]
    [InlineData(SignupStatus.AwaitingApproval)]
    [InlineData(SignupStatus.Waitlisted)]
    public async Task A_team_whose_signup_is_not_confirmed_cannot_be_connected(SignupStatus status)
    {
        var (admin, eventId) = await StartedTournamentAsync();
        var (teamId, _) = await NewTeamAsync();
        await SeedSignupAsync(eventId, teamId, status);

        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, (null, 1, "X", teamId)), HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_team_without_any_signup_cannot_be_connected_by_the_event_admin()
    {
        var (admin, eventId) = await StartedTournamentAsync();
        var (teamId, _) = await NewTeamAsync();

        await AssertStatusAsync(await SaveRankingAsync(admin, eventId, (null, 1, "X", teamId)), HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_signed_up_team_is_connected_with_attribution()
    {
        var (admin, eventId) = await StartedTournamentAsync();
        var (teamId, slug) = await NewTeamAsync();
        await SeedSignupAsync(eventId, teamId);

        var saved = await ReadJsonAsync(await SaveRankingAsync(admin, eventId, (null, 1, "typed name", teamId)));

        var placement = saved.GetProperty("placements")[0];
        Assert.Equal(slug, placement.GetProperty("teamSlug").GetString());
        Assert.Equal("Rheinfeuer", placement.GetProperty("name").GetString()); // the team's name, not the typed one

        var row = await WithDbAsync(db => db.TournamentPlacements.SingleAsync(p => p.TournamentResult.EventId == eventId));
        Assert.Equal("typed name", row.SourceName);
        Assert.NotNull(row.ConnectedByUserId);
        Assert.NotNull(row.ConnectedAt);
    }

    // --- What gets stored and shown ----------------------------------------------------------

    [Fact]
    public async Task Ties_are_stored_as_standard_competition_ranking()
    {
        var (admin, eventId) = await StartedTournamentAsync();

        var saved = await ReadJsonAsync(await SaveRankingAsync(admin, eventId,
            (null, 1, "Alpha", null), (null, 2, "Bravo", null), (null, 3, "Charlie", null),
            (null, 3, "Delta", null), (null, 4, "Echo", null)));

        var positions = saved.GetProperty("placements").EnumerateArray().Select(p => p.GetProperty("position").GetInt32());
        Assert.Equal([1, 2, 3, 3, 5], positions);
        Assert.Equal(5, saved.GetProperty("rankedCount").GetInt32());
    }

    [Fact]
    public async Task Any_signed_in_player_reads_the_ranking()
    {
        var (admin, eventId) = await StartedTournamentAsync();
        await SaveRankingAsync(admin, eventId, (null, 2, "Bravo", null), (null, 1, "Alpha", null));
        var (reader, _, _, _) = await NewUserAsync();

        var result = await ReadJsonAsync(await reader.GetAsync(EventResults(eventId)));

        Assert.Equal("Manual", result.GetProperty("source").GetString());
        Assert.Equal(["Alpha", "Bravo"], result.GetProperty("placements").EnumerateArray().Select(p => p.GetProperty("name").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.String, result.GetProperty("resultsChangedAt").ValueKind);
        Assert.False(result.GetProperty("viewer").GetProperty("canEdit").GetBoolean());
    }

    [Fact]
    public async Task Nothing_recorded_reads_as_an_empty_result_not_a_404()
    {
        var (admin, eventId) = await StartedTournamentAsync();

        var result = await ReadJsonAsync(await admin.GetAsync(EventResults(eventId)));

        Assert.Equal("None", result.GetProperty("source").GetString());
        Assert.Equal(0, result.GetProperty("placements").GetArrayLength());
        Assert.True(result.GetProperty("viewer").GetProperty("canEdit").GetBoolean());
    }

    [Fact]
    public async Task Clearing_removes_the_ranking_and_keeps_the_row()
    {
        var (admin, eventId) = await StartedTournamentAsync();
        await SaveRankingAsync(admin, eventId, (null, 1, "Alpha", null));

        await AssertStatusAsync(await admin.DeleteAsync($"{EventResults(eventId)}/ranking"), HttpStatusCode.NoContent);

        var result = await ReadJsonAsync(await admin.GetAsync(EventResults(eventId)));
        Assert.Equal("None", result.GetProperty("source").GetString());
        Assert.Equal(0, result.GetProperty("placements").GetArrayLength());
        Assert.True(await WithDbAsync(db => db.TournamentResults.AnyAsync(r => r.EventId == eventId)));
    }

    [Fact]
    public async Task An_individuals_tournament_takes_plain_names_and_has_no_signed_up_teams()
    {
        var (admin, eventId) = await StartedTournamentAsync("Individuals");

        await ReadJsonAsync(await SaveRankingAsync(admin, eventId, (null, 1, "Mixed team A", null)));
        var editor = await ReadJsonAsync(await admin.GetAsync($"{EventResults(eventId)}/editor"));

        Assert.Equal(0, editor.GetProperty("signedUpTeams").GetArrayLength());
    }

    [Fact]
    public async Task The_editor_lists_only_confirmed_teams()
    {
        var (admin, eventId) = await StartedTournamentAsync();
        var (joined, _) = await NewTeamAsync();
        var (awaiting, _) = await NewTeamAsync();
        var (waitlisted, _) = await NewTeamAsync();
        await SeedSignupAsync(eventId, joined);
        await SeedSignupAsync(eventId, awaiting, SignupStatus.AwaitingApproval);
        await SeedSignupAsync(eventId, waitlisted, SignupStatus.Waitlisted);

        var editor = await ReadJsonAsync(await admin.GetAsync($"{EventResults(eventId)}/editor"));

        var ids = editor.GetProperty("signedUpTeams").EnumerateArray().Select(t => t.GetProperty("teamId").GetGuid()).ToList();
        Assert.Equal([joined], ids);
    }
}
