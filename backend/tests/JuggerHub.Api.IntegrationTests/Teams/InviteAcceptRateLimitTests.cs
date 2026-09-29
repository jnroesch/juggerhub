using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Home;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Feature 064, FR-024: ten team-invitation acceptances per player per hour. A shared invite link stays
/// usable after someone joins with it, and since 064 every departure reaches each of the team's admins
/// by inbox, email and phone — so without a bound, joining by link and leaving is a way to message a
/// team's admins as often as one likes. Joining is the only step of that loop a player takes alone.
///
/// The test host has no Redis, so this exercises the in-memory fixed window — correct on a single host,
/// and partitioned per player, which is why other suites' accepts never share a bucket with these.
/// </summary>
[Collection("Teams")]
public sealed class InviteAcceptRateLimitTests
{
    private readonly JuggerHubApiFactory _factory;

    public InviteAcceptRateLimitTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task The_eleventh_join_by_invitation_in_an_hour_is_refused()
    {
        var (admin, _) = await NewUserAsync();
        var (teamId, slug) = await NewTeamAsync(admin);
        var token = await LinkTokenAsync(admin, slug);
        var (player, playerId) = await NewUserAsync();

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await player.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await player.DeleteAsync($"/api/v1/teams/{slug}/members/{playerId}")).StatusCode);
        }

        var eleventh = await player.PostAsync($"/api/v1/invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
        // Not added, and the link is untouched: anyone else can still use it.
        Assert.False(await HomeTestSupport.WithDbAsync(_factory, db =>
            db.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.UserId == playerId)));
        var (other, _) = await NewUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);
    }

    [Fact]
    public async Task Declining_and_previewing_are_never_limited()
    {
        var (admin, _) = await NewUserAsync();
        var (_, slug) = await NewTeamAsync(admin);
        var token = await LinkTokenAsync(admin, slug);
        var (player, playerId) = await NewUserAsync();

        for (var i = 0; i < 10; i++)
        {
            await player.PostAsync($"/api/v1/invitations/{token}/accept", null);
            await player.DeleteAsync($"/api/v1/teams/{slug}/members/{playerId}");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await player.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await player.GetAsync($"/api/v1/invitations/{token}")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await player.PostAsync($"/api/v1/invitations/{token}/decline", null)).StatusCode);
    }

    // --- helpers ------------------------------------------------------------

    private async Task<(HttpClient Client, Guid UserId)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: AuthTestHelpers.NewHandle());
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId);
    }

    private async Task<(Guid TeamId, string Slug)> NewTeamAsync(HttpClient admin)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var created = await admin.PostAsJsonAsync("/api/v1/teams", new
        {
            name = "Rheinfeuer " + slug[1..7],
            slug,
            type = "CityTeam",
            location = new { cityExternalId = "TEST:berlin" },
        });
        created.EnsureSuccessStatusCode();
        var teamId = await HomeTestSupport.WithDbAsync(_factory, db =>
            db.Teams.Where(t => t.Slug == slug).Select(t => t.Id).SingleAsync());
        return (teamId, slug);
    }

    private static async Task<string> LinkTokenAsync(HttpClient admin, string slug)
    {
        var link = await admin.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        link.EnsureSuccessStatusCode();
        return (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }
}
