using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Security;

/// <summary>
/// Feature 026 (US1) — teams, events, and all browse/search are authenticated-only. Every
/// anonymous read is refused at the server (SC-001/SC-002), while an authenticated caller
/// retains full access (SC-003). These assertions are independent of the UI.
/// </summary>
[Collection("Auth")]
public sealed class AnonymousAccessTests
{
    private readonly JuggerHubApiFactory _factory;

    public AnonymousAccessTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_reads_of_teams_events_and_browse_are_all_401()
    {
        var owner = await AuthedClientAsync();
        var (slug, eventId) = await SeedTeamAndEventAsync(owner);
        var anon = _factory.CreateClient();

        string[] gated =
        [
            "/api/v1/teams",
            $"/api/v1/teams/{slug}/public",
            "/api/v1/events",
            $"/api/v1/events/{eventId}",
            $"/api/v1/events/{eventId}/participants?group=joined",
            $"/api/v1/events/{eventId}/news",
            $"/api/v1/events/{eventId}/contacts",
            $"/api/v1/events/{eventId}/market/free-agents",
            "/api/v1/profiles",
        ];

        foreach (var url in gated)
        {
            var resp = await anon.GetAsync(url);
            Assert.True(resp.StatusCode == HttpStatusCode.Unauthorized,
                $"Expected 401 for anonymous GET {url} but got {(int)resp.StatusCode}.");
        }
    }

    [Fact]
    public async Task Authenticated_reads_of_the_same_endpoints_still_succeed()
    {
        var owner = await AuthedClientAsync();
        var (slug, eventId) = await SeedTeamAndEventAsync(owner);
        var viewer = await AuthedClientAsync();

        string[] gated =
        [
            "/api/v1/teams",
            $"/api/v1/teams/{slug}/public",
            "/api/v1/events",
            $"/api/v1/events/{eventId}",
            $"/api/v1/events/{eventId}/participants?group=joined",
            $"/api/v1/events/{eventId}/news",
            $"/api/v1/events/{eventId}/contacts",
            $"/api/v1/events/{eventId}/market/free-agents",
            "/api/v1/profiles",
        ];

        foreach (var url in gated)
        {
            var resp = await viewer.GetAsync(url);
            Assert.True(resp.StatusCode == HttpStatusCode.OK,
                $"Expected 200 for authenticated GET {url} but got {(int)resp.StatusCode}.");
        }
    }

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<(string Slug, Guid EventId)> SeedTeamAndEventAsync(HttpClient owner)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        (await owner.PostAsJsonAsync("/api/v1/teams",
            new { name = "Access Crew", slug, type = "Mixteam", city = (string?)null })).EnsureSuccessStatusCode();

        var create = await owner.PostAsJsonAsync("/api/v1/events", new
        {
            name = "Access Cup",
            type = "Workshop",
            description = "Seeded to assert authenticated-only reads.",
            startsAt = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            endsAt = DateTime.UtcNow.AddDays(30).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            locationKind = "Virtual",
            virtualLink = "https://zoom.us/j/1234567890",
            participantMode = "Individuals",
            participationLimit = 30,
            isPaid = false,
        });
        create.EnsureSuccessStatusCode();
        var eventId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (slug, eventId);
    }
}
