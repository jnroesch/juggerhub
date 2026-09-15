using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Parties;
using JuggerHub.Api.IntegrationTests.Recognition;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>Shares one Testcontainers Postgres + host across the tournament-results test classes.</summary>
[CollectionDefinition("Results")]
public sealed class ResultsCollection : ICollectionFixture<JuggerHubApiFactory>;

/// <summary>
/// Shared helpers for the tournament-results (050) integration tests. Builds on the party helpers
/// (users, teams, events) and seeds sign-ups directly: the signed-up-team rule reads only the
/// <see cref="EventSignup"/> row, so the party dance is not what these tests are about.
/// </summary>
public abstract class ResultsTestSupport : PartyTestSupport
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly SemaphoreSlim AdminGate = new(1, 1);

    protected ResultsTestSupport(JuggerHubApiFactory factory) : base(factory)
    {
    }

    protected static string EventResults(Guid eventId) => $"/api/v1/events/{eventId}/results";

    /// <summary>Create a tournament a month out (the creator becomes its admin); returns its id.</summary>
    protected async Task<Guid> CreateTournamentAsync(
        HttpClient client, string participantMode = "Teams", string type = "Tournament")
    {
        var teams = participantMode == "Teams";
        var resp = await client.PostAsJsonAsync("/api/v1/events", new
        {
            name = "Hanse Cup",
            type,
            description = "A weekend of Jugger on the harbour meadows.",
            startsAt = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            endsAt = DateTime.UtcNow.AddDays(31).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            locationKind = "Virtual",
            venueName = (string?)null,
            street = (string?)null,
            postalCode = (string?)null,
            location = (object?)null,
            virtualLink = "https://jugger.example/hanse",
            participantMode,
            participationLimit = 24,
            rosterCap = teams ? 8 : (int?)null,
            isPaid = false,
            feeAmount = (decimal?)null,
            feeCurrency = (string?)null,
            feeRecipientName = (string?)null,
            feeIban = (string?)null,
            feePaymentDeadline = (string?)null,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Move an event in time (e.g. into the past so results may be recorded).</summary>
    protected async Task SetEventDatesAsync(Guid eventId, DateTime startsAt, DateTime endsAt)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Events.Where(e => e.Id == eventId).ExecuteUpdateAsync(s => s
            .SetProperty(e => e.StartsAt, startsAt)
            .SetProperty(e => e.EndsAt, endsAt)
            .SetProperty(e => e.ModifiedDate, DateTime.UtcNow));
    }

    /// <summary>Start the tournament an hour ago (it is running, so results may be recorded).</summary>
    protected Task StartEventAsync(Guid eventId) =>
        SetEventDatesAsync(eventId, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(8));

    protected static async Task CancelEventAsync(HttpClient client, Guid eventId) =>
        (await client.PostAsync($"/api/v1/events/{eventId}/cancel", null)).EnsureSuccessStatusCode();

    /// <summary>Seed a team's sign-up for an event with the given status.</summary>
    protected async Task SeedSignupAsync(Guid eventId, Guid teamId, SignupStatus status = SignupStatus.Joined)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EventSignups.Add(new EventSignup { EventId = eventId, TeamId = teamId, Status = status });
        await db.SaveChangesAsync();
    }

    /// <summary>A team owned by a throwaway admin — for tests that only need the team to exist.</summary>
    protected async Task<(Guid TeamId, string Slug)> NewTeamAsync()
    {
        var (owner, _, _, _) = await NewUserAsync();
        return await CreateTeamAsync(owner);
    }

    /// <summary>
    /// A signed-in client for the configured platform admin. Unlike the shared admin helper, it
    /// decides from THIS factory's database whether the admin exists yet, so it is correct no
    /// matter which test collection ran first.
    /// </summary>
    protected async Task<(HttpClient Client, Guid UserId)> PlatformAdminClientAsync()
    {
        await AdminGate.WaitAsync();
        try
        {
            var exists = await WithDbAsync(db => db.Users.AnyAsync(u => u.Email == RecognitionTestSupport.AdminEmail));
            if (!exists)
            {
                var setup = Factory.CreateClient();
                await AuthTestHelpers.RegisterAndVerifyAsync(setup, Factory, email: RecognitionTestSupport.AdminEmail);
                await RecognitionTestSupport.RunAdminRoleSyncAsync(Factory);
            }
        }
        finally
        {
            AdminGate.Release();
        }

        var adminId = await WithDbAsync(db =>
            db.Users.Where(u => u.Email == RecognitionTestSupport.AdminEmail).Select(u => u.Id).SingleAsync());
        var client = Factory.CreateClient();
        (await AuthTestHelpers.LoginAsync(client, RecognitionTestSupport.AdminEmail, AuthTestHelpers.ValidPassword))
            .EnsureSuccessStatusCode();
        return (client, adminId);
    }

    protected async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected async Task WithDbAsync(Func<AppDbContext, Task> work)
    {
        using var scope = Factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>PUT a ranking; rows are (id, position, name, teamId).</summary>
    protected static Task<HttpResponseMessage> SaveRankingAsync(
        HttpClient client, Guid eventId, params (Guid? Id, int Position, string Name, Guid? TeamId)[] rows) =>
        client.PutAsJsonAsync($"{EventResults(eventId)}/ranking", new
        {
            placements = rows.Select(r => new { id = r.Id, position = r.Position, name = r.Name, teamId = r.TeamId }),
        });

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    protected static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected}, got {(int)response.StatusCode}: {body}");
        }
    }

    /// <summary>Read a committed Tugeny response (contracts/tugeny-samples).</summary>
    internal static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Results", "Fixtures", name));

    /// <summary>
    /// A host whose Tugeny client talks to <paramref name="stub"/>. The shipped registration — the
    /// shared resilience pipeline and the size guard — stays in place; only the transport changes.
    /// Each host has its own pipeline, so a test that trips the breaker trips it only for itself.
    /// </summary>
    protected WebApplicationFactory<Program> HostWith(HttpMessageHandler stub) =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient(TugenyOptions.ResilienceName).ConfigurePrimaryHttpMessageHandler(() => stub)));

    /// <summary>Sign an existing account in on another host (the database and signing keys are shared).</summary>
    protected static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, string email)
    {
        var client = host.CreateClient();
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return client;
    }
}
