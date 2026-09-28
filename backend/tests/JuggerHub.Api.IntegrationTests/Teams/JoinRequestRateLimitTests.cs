using System.Net;
using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Home;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Feature 058, FR-023: ten join requests per player per hour. Anyone can ask any team, and every
/// request now reaches each admin by inbox, email and phone, so without a bound requesting and
/// withdrawing is a way to message a team's admins as often as one likes.
///
/// The test host has no Redis, so this exercises the in-memory fixed window — correct on a single
/// host, and partitioned per player, which is also why other suites' requests never share a bucket
/// with these.
/// </summary>
[Collection("Teams")]
public sealed class JoinRequestRateLimitTests
{
    private readonly JuggerHubApiFactory _factory;

    public JoinRequestRateLimitTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task The_eleventh_request_in_an_hour_is_refused()
    {
        var (admin, adminId) = await NewUserAsync();
        var slug = await NewTeamAsync(admin);
        var (player, playerId) = await NewUserAsync();

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await player.PostAsync($"/api/v1/teams/{slug}/join-requests", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await player.DeleteAsync($"/api/v1/teams/{slug}/join-requests/mine")).StatusCode);
        }

        var eleventh = await player.PostAsync($"/api/v1/teams/{slug}/join-requests", null);

        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
        // Nothing stored, nobody told: every earlier alert went with its withdrawal, and the refused
        // request created none.
        Assert.False(await HomeTestSupport.WithDbAsync(_factory, db =>
            db.TeamJoinRequests.AnyAsync(r => r.UserId == playerId)));
        Assert.Equal(0, await HomeTestSupport.WithDbAsync(_factory, db =>
            db.Notifications.CountAsync(n => n.RecipientUserId == adminId && n.Type == NotificationType.TeamJoinRequest)));
        // Withdrawing is never limited.
        Assert.Equal(HttpStatusCode.NoContent, (await player.DeleteAsync($"/api/v1/teams/{slug}/join-requests/mine")).StatusCode);
    }

    [Fact]
    public async Task The_limit_is_per_player()
    {
        var (admin, _) = await NewUserAsync();
        var slug = await NewTeamAsync(admin);
        var (busy, _) = await NewUserAsync();
        var (other, _) = await NewUserAsync();

        for (var i = 0; i < 10; i++)
        {
            await busy.PostAsync($"/api/v1/teams/{slug}/join-requests", null);
            await busy.DeleteAsync($"/api/v1/teams/{slug}/join-requests/mine");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await busy.PostAsync($"/api/v1/teams/{slug}/join-requests", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await other.PostAsync($"/api/v1/teams/{slug}/join-requests", null)).StatusCode);
    }

    // --- helpers ------------------------------------------------------------

    private async Task<(HttpClient Client, Guid UserId)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: AuthTestHelpers.NewHandle());
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId);
    }

    private static async Task<string> NewTeamAsync(HttpClient admin)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var created = await admin.PostAsJsonAsync("/api/v1/teams", new
        {
            name = "Rheinfeuer",
            slug,
            type = "CityTeam",
            location = new { cityExternalId = "TEST:berlin" },
        });
        created.EnsureSuccessStatusCode();
        return slug;
    }
}
