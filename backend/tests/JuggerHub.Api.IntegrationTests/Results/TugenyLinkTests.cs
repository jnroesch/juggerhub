using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>Linking a tournament event to its Tugeny tournament (feature 050, US4 / FR-014, FR-015, FR-018).</summary>
[Collection("Results")]
public sealed class TugenyLinkTests : ResultsTestSupport
{
    private const string Address = "https://tugeny.org/tournaments/25-deutsche-meisterschaft/all-teams";

    public TugenyLinkTests(JuggerHubApiFactory factory) : base(factory)
    {
    }

    private async Task<(Guid EventId, string Email)> TournamentAsync()
    {
        var (admin, _, _, email) = await NewUserAsync();
        return (await CreateTournamentAsync(admin), email);
    }

    private static Task<HttpResponseMessage> LinkAsync(HttpClient client, Guid eventId, string address = Address) =>
        client.PutAsJsonAsync($"{EventResults(eventId)}/tugeny-link", new { address });

    [Fact]
    public async Task Linking_stores_the_tournament_and_shows_its_links_before_the_start()
    {
        var (eventId, email) = await TournamentAsync();
        var stub = TugenyStub.Finalized();
        using var host = HostWith(stub);
        var admin = await SignInAsync(host, email);

        var linked = await ReadJsonAsync(await LinkAsync(admin, eventId));

        Assert.Equal(200, linked.GetProperty("tournamentId").GetInt32());
        Assert.Equal("25. Deutsche Meisterschaft", linked.GetProperty("name").GetString());
        Assert.Equal("2024-09-21", linked.GetProperty("startDate").GetString());
        // The collection shares one database, where other tests link the same Tugeny tournament:
        // the flag must match the database, whatever ran first.
        var elsewhere = await WithDbAsync(db => db.TournamentResults.AnyAsync(r => r.TugenyTournamentId == 200 && r.EventId != eventId));
        Assert.Equal(elsewhere, linked.GetProperty("linkedElsewhere").GetBoolean());

        var result = await ReadJsonAsync(await admin.GetAsync(EventResults(eventId)));
        var tugeny = result.GetProperty("tugeny");
        Assert.EndsWith("/tournaments/25-deutsche-meisterschaft/live-view", tugeny.GetProperty("liveUrl").GetString());
        Assert.Equal("None", result.GetProperty("source").GetString());
    }

    [Fact]
    public async Task An_unknown_tournament_is_not_found()
    {
        var (eventId, email) = await TournamentAsync();
        using var host = HostWith(TugenyStub.Unknown());
        var admin = await SignInAsync(host, email);

        await AssertStatusAsync(await LinkAsync(admin, eventId), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_address_that_is_not_tugeny_is_refused_without_calling_out()
    {
        var (eventId, email) = await TournamentAsync();
        var stub = TugenyStub.Finalized();
        using var host = HostWith(stub);
        var admin = await SignInAsync(host, email);

        await AssertStatusAsync(await LinkAsync(admin, eventId, "https://evil.example/tournaments/x"), HttpStatusCode.BadRequest);
        Assert.Equal(0, stub.Calls);
    }

    [Fact]
    public async Task Tugeny_being_down_is_a_503_and_stores_nothing()
    {
        var (eventId, email) = await TournamentAsync();
        using var host = HostWith(TugenyStub.AlwaysFails());
        var admin = await SignInAsync(host, email);

        var response = await LinkAsync(admin, eventId);

        await AssertStatusAsync(response, HttpStatusCode.ServiceUnavailable);
        Assert.False(await WithDbAsync(db => db.TournamentResults.AnyAsync(r => r.EventId == eventId && r.TugenyTournamentId != null)));
    }

    [Fact]
    public async Task A_cancelled_tournament_cannot_be_linked()
    {
        var (admin0, _, _, email) = await NewUserAsync();
        var eventId = await CreateTournamentAsync(admin0);
        await CancelEventAsync(admin0, eventId);
        using var host = HostWith(TugenyStub.Finalized());
        var admin = await SignInAsync(host, email);

        await AssertStatusAsync(await LinkAsync(admin, eventId), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Only_an_admin_of_the_event_may_link_it()
    {
        var (eventId, _) = await TournamentAsync();
        var (_, _, _, strangerEmail) = await NewUserAsync();
        using var host = HostWith(TugenyStub.Finalized());
        var stranger = await SignInAsync(host, strangerEmail);

        await AssertStatusAsync(await LinkAsync(stranger, eventId), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_same_tugeny_tournament_on_two_events_is_saved_but_flagged()
    {
        var (first, email) = await TournamentAsync();
        using var host = HostWith(TugenyStub.Finalized());
        var admin = await SignInAsync(host, email);
        var second = await CreateTournamentAsync(admin);

        await ReadJsonAsync(await LinkAsync(admin, first));
        var linked = await ReadJsonAsync(await LinkAsync(admin, second));

        Assert.True(linked.GetProperty("linkedElsewhere").GetBoolean());
    }

    [Fact]
    public async Task Removing_the_link_keeps_the_results()
    {
        var (eventId, email) = await TournamentAsync();
        using var host = HostWith(TugenyStub.Finalized());
        var admin = await SignInAsync(host, email);
        await ReadJsonAsync(await LinkAsync(admin, eventId));
        await StartEventAsync(eventId);
        await ReadJsonAsync(await SaveRankingAsync(admin, eventId, (null, 1, "Alpha", null)));

        await AssertStatusAsync(await admin.DeleteAsync($"{EventResults(eventId)}/tugeny-link"), HttpStatusCode.NoContent);

        var row = await WithDbAsync(db => db.TournamentResults.Include(r => r.Placements).SingleAsync(r => r.EventId == eventId));
        Assert.Null(row.TugenyTournamentId);
        Assert.Null(row.TugenySlug);
        Assert.Single(row.Placements);
    }
}
