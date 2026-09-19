using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Push;

/// <summary>
/// Registering and removing the browser a member is using (feature 055, US1). Exercises the real
/// API and Postgres; nothing here delivers a notification.
/// </summary>
[Collection("Teams")]
public sealed class PushSubscriptionTests
{
    private readonly JuggerHubApiFactory _factory;

    public PushSubscriptionTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_key_is_served_to_a_signed_in_member()
    {
        var (client, _) = await NewUserAsync();

        var dto = await client.GetFromJsonAsync<JsonElement>("/api/v1/push/public-key");

        Assert.Equal(JuggerHubApiFactory.TestVapidPublicKey, dto.GetProperty("publicKey").GetString());
    }

    [Fact]
    public async Task Registering_twice_creates_one_row_and_refreshes_its_keys()
    {
        var (client, userId) = await NewUserAsync();
        var endpoint = NewEndpoint();

        Assert.Equal(HttpStatusCode.NoContent, (await RegisterAsync(client, endpoint, auth: "auth-one")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RegisterAsync(client, endpoint, auth: "auth-two")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.PushSubscriptions.AsNoTracking()
            .Where(s => s.Endpoint == endpoint).ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(userId, row.UserId);
        // A browser may rotate its keys without changing the endpoint, so the second registration
        // must update rather than be ignored.
        Assert.Equal("auth-two", row.Auth);
    }

    [Fact]
    public async Task An_endpoint_held_by_another_account_moves_to_the_caller()
    {
        // The shared-device case: one phone, two people, one after the other. A push endpoint
        // identifies a BROWSER, not a person, so the row must move — otherwise the previous owner
        // keeps receiving the new owner's notifications.
        var (first, firstId) = await NewUserAsync();
        var (second, secondId) = await NewUserAsync();
        var endpoint = NewEndpoint();

        await RegisterAsync(first, endpoint);
        await RegisterAsync(second, endpoint);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.PushSubscriptions.AsNoTracking()
            .Where(s => s.Endpoint == endpoint).ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(secondId, row.UserId);
        Assert.NotEqual(firstId, row.UserId);
    }

    [Fact]
    public async Task Removing_is_scoped_to_the_caller_and_is_idempotent()
    {
        var (owner, ownerId) = await NewUserAsync();
        var (stranger, _) = await NewUserAsync();
        var endpoint = NewEndpoint();
        await RegisterAsync(owner, endpoint);

        // Someone else's endpoint: a no-op that never admits the row exists.
        var strangerRemove = await RemoveAsync(stranger, endpoint);
        Assert.Equal(HttpStatusCode.NoContent, strangerRemove.StatusCode);
        Assert.True(await ExistsAsync(endpoint));

        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(owner, endpoint)).StatusCode);
        Assert.False(await ExistsAsync(endpoint));

        // Removing again is still NoContent — nothing to report either way.
        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(owner, endpoint)).StatusCode);
    }

    [Fact]
    public async Task A_non_https_endpoint_is_rejected()
    {
        var (client, _) = await NewUserAsync();

        var resp = await RegisterAsync(client, "http://push.example.com/not-secure");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Every_push_endpoint_requires_a_signed_in_member()
    {
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/push/public-key")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RegisterAsync(anon, NewEndpoint())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RemoveAsync(anon, NewEndpoint())).StatusCode);
    }

    // --- helpers ------------------------------------------------------------

    private static string NewEndpoint() => $"https://push.example.com/send/{Guid.NewGuid():N}";

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string endpoint, string auth = "auth-secret") =>
        client.PostAsJsonAsync("/api/v1/push/subscriptions", new
        {
            endpoint,
            p256dh = "device-public-key",
            auth,
            deviceLabel = "Chrome on Android",
        });

    private static Task<HttpResponseMessage> RemoveAsync(HttpClient client, string endpoint) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push/subscriptions")
        {
            Content = JsonContent.Create(new { endpoint }),
        });

    private async Task<bool> ExistsAsync(string endpoint)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PushSubscriptions.AsNoTracking().AnyAsync(s => s.Endpoint == endpoint);
    }

    private async Task<(HttpClient Client, Guid UserId)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(
            client, _factory, handle: AuthTestHelpers.NewHandle());
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword))
            .EnsureSuccessStatusCode();
        return (client, userId);
    }
}
