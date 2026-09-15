using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>
/// The platform admins' placement queue (feature 050, US6 / FR-024–FR-026): teams that played
/// without a JuggerHub sign-up get their placements here, one at a time, after a person checked.
/// </summary>
[Collection("Results")]
public sealed class AdminPlacementTests : ResultsTestSupport
{
    private const string Queue = "/api/v1/admin/results/placements";

    public AdminPlacementTests(JuggerHubApiFactory factory) : base(factory)
    {
    }

    /// <summary>A past tournament recorded with plain names only; returns the placement ids by name.</summary>
    private async Task<(Guid EventId, Dictionary<string, Guid> Ids)> PastTournamentAsync(params string[] names)
    {
        var (admin, _, _, _) = await NewUserAsync();
        var eventId = await CreateTournamentAsync(admin);
        await SetEventDatesAsync(eventId, DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddYears(-1).AddDays(1));
        await ReadJsonAsync(await SaveRankingAsync(admin, eventId,
            names.Select((n, i) => ((Guid?)null, i + 1, n, (Guid?)null)).ToArray()));

        var ids = await WithDbAsync(db => db.TournamentPlacements
            .Where(p => p.TournamentResult.EventId == eventId)
            .ToDictionaryAsync(p => p.SourceName, p => p.Id));
        return (eventId, ids);
    }

    private static Task<HttpResponseMessage> ConnectAsync(HttpClient client, Guid placementId, string teamSlug) =>
        client.PutAsJsonAsync($"{Queue}/{placementId}/team", new { teamSlug });

    [Fact]
    public async Task Only_platform_admins_reach_the_queue()
    {
        var (_, ids) = await PastTournamentAsync("Ecplise");
        var (teamOwner, _, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(teamOwner);

        // A team admin — the very team the name might mean — still cannot claim it.
        await AssertStatusAsync(await teamOwner.GetAsync(Queue), HttpStatusCode.Forbidden);
        await AssertStatusAsync(await ConnectAsync(teamOwner, ids["Ecplise"], slug), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Connecting_attributes_the_placement_and_changes_exactly_one_row()
    {
        var (first, firstIds) = await PastTournamentAsync("Ecplise", "Kiel Guests");
        var (second, secondIds) = await PastTournamentAsync("Ecplise");
        var (_, slug) = await NewTeamAsync();
        var (admin, adminId) = await PlatformAdminClientAsync();

        var connected = await ReadJsonAsync(await ConnectAsync(admin, firstIds["Ecplise"], slug));

        Assert.Equal(slug, connected.GetProperty("team").GetProperty("slug").GetString());
        Assert.Equal("Ecplise", connected.GetProperty("sourceName").GetString());
        Assert.Equal("Rheinfeuer", connected.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.String, connected.GetProperty("connectedAt").ValueKind);

        var rows = await WithDbAsync(db => db.TournamentPlacements
            .Where(p => p.TournamentResult.EventId == first || p.TournamentResult.EventId == second)
            .ToListAsync());
        Assert.Single(rows, r => r.TeamId != null);
        Assert.Equal(adminId, rows.Single(r => r.TeamId != null).ConnectedByUserId);
        // The same name in another tournament stays a plain name until someone checks it too (FR-025).
        Assert.Null(rows.Single(r => r.Id == secondIds["Ecplise"]).TeamId);
    }

    [Fact]
    public async Task A_team_holds_one_placement_per_ranking()
    {
        var (_, ids) = await PastTournamentAsync("Alpha", "Also Alpha");
        var (_, slug) = await NewTeamAsync();
        var (admin, _) = await PlatformAdminClientAsync();
        await ReadJsonAsync(await ConnectAsync(admin, ids["Alpha"], slug));

        await AssertStatusAsync(await ConnectAsync(admin, ids["Also Alpha"], slug), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unknown_placements_and_teams_are_not_found()
    {
        var (_, ids) = await PastTournamentAsync("Alpha");
        var (admin, _) = await PlatformAdminClientAsync();

        await AssertStatusAsync(await ConnectAsync(admin, Guid.NewGuid(), "rheinfeuer"), HttpStatusCode.NotFound);
        await AssertStatusAsync(await ConnectAsync(admin, ids["Alpha"], "no-such-team-050"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Disconnecting_restores_the_recorded_name_and_is_idempotent()
    {
        var (_, ids) = await PastTournamentAsync("Ecplise");
        var (_, slug) = await NewTeamAsync();
        var (admin, _) = await PlatformAdminClientAsync();
        await ReadJsonAsync(await ConnectAsync(admin, ids["Ecplise"], slug));

        var first = await ReadJsonAsync(await admin.DeleteAsync($"{Queue}/{ids["Ecplise"]}/team"));
        await ReadJsonAsync(await admin.DeleteAsync($"{Queue}/{ids["Ecplise"]}/team"));

        Assert.Equal("Ecplise", first.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("team").ValueKind);
        var history = await ReadJsonAsync(await admin.GetAsync($"/api/v1/teams/{slug}/placements"));
        Assert.Equal(0, history.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task The_queue_lists_unconnected_placements_newest_first_and_searches_names()
    {
        var (_, ids) = await PastTournamentAsync("Zwölf Überflieger 050");
        var (admin, _) = await PlatformAdminClientAsync();

        // Accent-insensitive, and the default view is the unconnected work queue.
        var page = await ReadJsonAsync(await admin.GetAsync($"{Queue}?q=uberflieger 050"));

        var item = page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == ids["Zwölf Überflieger 050"]);
        Assert.Equal(1, item.GetProperty("position").GetInt32());
        Assert.False(item.GetProperty("fromTugeny").GetBoolean());

        var connectedOnly = await ReadJsonAsync(await admin.GetAsync($"{Queue}?connected=true&q=uberflieger 050"));
        Assert.Equal(0, connectedOnly.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task An_erased_connector_reads_as_a_former_player()
    {
        var (_, ids) = await PastTournamentAsync("Ecplise");
        var (_, slug) = await NewTeamAsync();
        var (admin, _) = await PlatformAdminClientAsync();
        await ReadJsonAsync(await ConnectAsync(admin, ids["Ecplise"], slug));

        // Attribute the connection to a player who then erases their account (037): the user row
        // survives, neutralised, so the Restrict reference holds and projects to the placeholder.
        var (leaver, leaverId, _, _) = await NewUserAsync();
        await WithDbAsync(db => db.TournamentPlacements.Where(p => p.Id == ids["Ecplise"])
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ConnectedByUserId, leaverId)));
        (await leaver.PostAsJsonAsync("/api/v1/account/deletion",
            new { password = AuthTestHelpers.ValidPassword, confirmation = "DELETE" })).EnsureSuccessStatusCode();

        var page = await ReadJsonAsync(await admin.GetAsync($"{Queue}?connected=true&q=Rheinfeuer"));
        var item = page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == ids["Ecplise"]);

        var connectedBy = item.GetProperty("connectedBy").GetString();
        Assert.False(string.IsNullOrWhiteSpace(connectedBy));
        Assert.DoesNotContain(leaverId.ToString(), connectedBy!);
    }
}
