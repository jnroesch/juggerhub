using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Events;

/// <summary>
/// Events (006). US1 — creation via the wizard: the create validation matrix and the creator
/// becoming the first admin. Exercises the real API + Postgres container.
/// </summary>
[Collection("Events")]
public sealed class EventTests
{
    private readonly JuggerHubApiFactory _factory;

    public EventTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US1: create ----------------------------------------------------------

    [Fact]
    public async Task Create_in_person_paid_teams_event_makes_creator_admin()
    {
        var (client, _, _, _) = await NewUserAsync();

        var resp = await client.PostAsJsonAsync("/api/v1/events", ValidInPersonPaidTeams());

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Tournament", dto.GetProperty("type").GetString());
        Assert.Equal("Teams", dto.GetProperty("participantMode").GetString());
        Assert.Equal("InPerson", dto.GetProperty("locationKind").GetString());
        Assert.Equal("Deutschland", dto.GetProperty("country").GetString());
        Assert.True(dto.GetProperty("isPaid").GetBoolean());
        Assert.Equal(0, dto.GetProperty("occupiedSpots").GetInt32());
        Assert.False(dto.GetProperty("isFull").GetBoolean());
        Assert.True(dto.GetProperty("viewer").GetProperty("isAdmin").GetBoolean());
        Assert.NotEqual(Guid.Empty, dto.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Create_virtual_free_individuals_event()
    {
        var (client, _, _, _) = await NewUserAsync();

        var resp = await client.PostAsJsonAsync("/api/v1/events", ValidVirtualFreeIndividuals());

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Virtual", dto.GetProperty("locationKind").GetString());
        Assert.Equal("Individuals", dto.GetProperty("participantMode").GetString());
        Assert.False(dto.GetProperty("isPaid").GetBoolean());
        Assert.False(string.IsNullOrEmpty(dto.GetProperty("virtualLink").GetString()));
    }

    [Fact]
    public async Task End_before_start_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidInPersonPaidTeams(), new { startsAt = "2026-09-06T18:00:00Z", endsAt = "2026-09-05T09:00:00Z" });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task In_person_without_country_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidInPersonPaidTeams(), new { country = (string?)null });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Virtual_without_link_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidVirtualFreeIndividuals(), new { virtualLink = (string?)null });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Non_positive_limit_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidInPersonPaidTeams(), new { participationLimit = 0 });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Paid_without_recipient_or_iban_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidInPersonPaidTeams(), new { feeRecipientName = (string?)null, feeIban = (string?)null });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Other_type_without_custom_label_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidVirtualFreeIndividuals(), new { type = "Other", customTypeLabel = (string?)null });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_create_is_rejected()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/events", ValidVirtualFreeIndividuals());

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // --- US2: public page -----------------------------------------------------

    [Fact]
    public async Task Public_event_detail_is_readable_anonymously()
    {
        var (creator, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(creator, ValidInPersonPaidTeams());

        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/api/v1/events/{id}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Berlin Cup", dto.GetProperty("name").GetString());
        Assert.Equal("Deutschland", dto.GetProperty("country").GetString());
        Assert.Equal("DE89370400440532013000", dto.GetProperty("feeIban").GetString());
        var viewer = dto.GetProperty("viewer");
        Assert.False(viewer.GetProperty("isAuthenticated").GetBoolean());
        Assert.False(viewer.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Unknown_event_detail_is_404()
    {
        var anon = _factory.CreateClient();

        var resp = await anon.GetAsync($"/api/v1/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Creator_sees_admin_viewer_relation()
    {
        var (creator, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(creator, ValidVirtualFreeIndividuals());

        var dto = await creator.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}");

        var viewer = dto.GetProperty("viewer");
        Assert.True(viewer.GetProperty("isAuthenticated").GetBoolean());
        Assert.True(viewer.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Participant_groups_are_public_and_start_empty()
    {
        var (creator, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(creator, ValidVirtualFreeIndividuals());
        var anon = _factory.CreateClient();

        var joined = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/participants?group=joined");
        Assert.Equal(0, joined.GetProperty("totalCount").GetInt32());

        var bad = await anon.GetAsync($"/api/v1/events/{id}/participants?group=nope");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var unknown = await anon.GetAsync($"/api/v1/events/{Guid.NewGuid()}/participants?group=joined");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // --- US3: sign up ---------------------------------------------------------

    [Fact]
    public async Task Free_individual_signup_joins()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (u, _, _, _) = await NewUserAsync();

        var resp = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Joined", dto.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Paid_individual_signup_awaits_approval()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsPaid(4));
        var (u, _, _, _) = await NewUserAsync();

        var resp = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AwaitingApproval", dto.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Full_event_puts_signup_on_waitlist()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(1));
        var (u1, _, _, _) = await NewUserAsync();
        var (u2, _, _, _) = await NewUserAsync();

        var r1 = await u1.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var r2 = await u2.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        Assert.Equal("Joined", (await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal("Waitlisted", (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Team_admin_enters_team_but_non_admin_cannot()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var teamId = await CreateTeamAsync(admin);
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, TeamsFree(4));

        var ok = await admin.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal("Joined", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        // A different user who does not administer that team cannot enter it (fresh event).
        var id2 = await CreateEventAsync(org, TeamsFree(4));
        var (outsider, _, _, _) = await NewUserAsync();
        var forbidden = await outsider.PostAsJsonAsync($"/api/v1/events/{id2}/signup", new { teamId });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Mode_mismatch_is_rejected()
    {
        var (u, _, _, _) = await NewUserAsync();
        var teamId = await CreateTeamAsync(u);

        var individualsId = await CreateEventAsync(u, IndividualsFree(4));
        var teamsId = await CreateEventAsync(u, TeamsFree(4));

        var teamIntoIndividuals = await u.PostAsJsonAsync($"/api/v1/events/{individualsId}/signup", new { teamId });
        var individualIntoTeams = await u.PostAsJsonAsync($"/api/v1/events/{teamsId}/signup", new { teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.BadRequest, teamIntoIndividuals.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, individualIntoTeams.StatusCode);
    }

    [Fact]
    public async Task Duplicate_signup_is_rejected()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (u, _, _, _) = await NewUserAsync();

        var first = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var second = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Withdraw_releases_the_spot()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (u, _, _, _) = await NewUserAsync();

        var join = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var signupId = (await join.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var withdraw = await u.DeleteAsync($"/api/v1/events/{id}/signup/{signupId}");
        Assert.Equal(HttpStatusCode.NoContent, withdraw.StatusCode);

        var anon = _factory.CreateClient();
        var joined = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/participants?group=joined");
        Assert.Equal(0, joined.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Signup_on_ended_event_is_refused()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, EndedIndividuals());
        var (u, _, _, _) = await NewUserAsync();

        var resp = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Concurrent_last_spot_signups_never_exceed_the_limit()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(1));

        var clients = new List<HttpClient>();
        for (var i = 0; i < 5; i++)
        {
            var (c, _, _, _) = await NewUserAsync();
            clients.Add(c);
        }

        var results = await Task.WhenAll(clients.Select(c =>
            c.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null })));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var anon = _factory.CreateClient();
        var joined = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/participants?group=joined");
        var waitlist = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/participants?group=waitlist");

        Assert.Equal(1, joined.GetProperty("totalCount").GetInt32());
        Assert.Equal(4, waitlist.GetProperty("totalCount").GetInt32());
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<Guid> CreateTeamAsync(HttpClient client)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var resp = await client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Crew", slug, type = "Mixteam", city = (string?)null });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Teams.Where(t => t.Slug == slug).Select(t => t.Id).FirstAsync();
    }

    private static object IndividualsFree(int limit) =>
        Merge(ValidVirtualFreeIndividuals(), new { participationLimit = limit });

    private static object IndividualsPaid(int limit) =>
        Merge(ValidVirtualFreeIndividuals(), new
        {
            participationLimit = limit,
            isPaid = true,
            feeRecipientName = "Organiser e.V.",
            feeIban = "DE89370400440532013000",
            feeCurrency = "EUR",
        });

    private static object TeamsFree(int limit) =>
        Merge(ValidVirtualFreeIndividuals(), new { participantMode = "Teams", participationLimit = limit });

    private static object EndedIndividuals() =>
        Merge(ValidVirtualFreeIndividuals(), new { startsAt = "2020-01-01T09:00:00Z", endsAt = "2020-01-01T18:00:00Z" });

    private async Task<Guid> CreateEventAsync(HttpClient client, object body)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/events", body);
        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return dto.GetProperty("id").GetGuid();
    }

    private static object ValidInPersonPaidTeams() => new
    {
        name = "Berlin Cup",
        type = "Tournament",
        description = "Two days of open Jugger on the old airfield. All divisions welcome.",
        startsAt = "2026-09-05T09:00:00Z",
        endsAt = "2026-09-06T18:00:00Z",
        locationKind = "InPerson",
        venueName = "Altes Flugfeld",
        street = "Hauptstrasse 1",
        postalCode = "10115",
        city = "Berlin",
        country = "Deutschland",
        virtualLink = (string?)null,
        participantMode = "Teams",
        participationLimit = 16,
        isPaid = true,
        feeAmount = 40m,
        feeCurrency = "EUR",
        feeRecipientName = "JSC Berlin e.V.",
        feeIban = "DE89370400440532013000",
        feePaymentDeadline = "2026-08-20",
    };

    private static object ValidVirtualFreeIndividuals() => new
    {
        name = "Pompfen Skills Session",
        type = "Workshop",
        description = "Online technique clinic for runners and chains.",
        startsAt = "2026-07-20T18:00:00Z",
        endsAt = "2026-07-20T20:00:00Z",
        locationKind = "Virtual",
        venueName = (string?)null,
        street = (string?)null,
        postalCode = (string?)null,
        city = (string?)null,
        country = (string?)null,
        virtualLink = "https://zoom.us/j/1234567890",
        participantMode = "Individuals",
        participationLimit = 30,
        isPaid = false,
        feeAmount = (decimal?)null,
        feeCurrency = (string?)null,
        feeRecipientName = (string?)null,
        feeIban = (string?)null,
        feePaymentDeadline = (string?)null,
    };

    /// <summary>Serialize the base request, overlay the override object's properties, return a JsonElement body.</summary>
    private static JsonElement Merge(object baseBody, object overrides)
    {
        var map = new Dictionary<string, JsonElement>();
        foreach (var p in JsonSerializer.SerializeToElement(baseBody).EnumerateObject())
        {
            map[p.Name] = p.Value.Clone();
        }

        foreach (var p in JsonSerializer.SerializeToElement(overrides).EnumerateObject())
        {
            map[p.Name] = p.Value.Clone();
        }

        return JsonSerializer.SerializeToElement(map);
    }

    private async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }
}
