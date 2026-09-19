using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Push;

/// <summary>
/// What happens to a device registration over its life (feature 055, US4): removal, the retention
/// sweep for devices nothing is ever delivered to, and account deletion.
/// </summary>
[Collection("Teams")]
public sealed class PushSubscriptionLifecycleTests
{
    private readonly JuggerHubApiFactory _factory;

    public PushSubscriptionLifecycleTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Removing_one_device_leaves_the_members_other_device_registered()
    {
        var (client, userId) = await NewUserAsync();
        var phone = NewEndpoint();
        var laptop = NewEndpoint();
        await RegisterAsync(client, phone);
        await RegisterAsync(client, laptop);

        await RemoveAsync(client, phone);

        var endpoints = await EndpointsForAsync(userId);
        Assert.DoesNotContain(phone, endpoints);
        Assert.Contains(laptop, endpoints);
    }

    [Fact]
    public async Task The_sweep_deletes_only_devices_past_the_idle_window()
    {
        var (client, userId) = await NewUserAsync();
        var fresh = NewEndpoint();
        var stale = NewEndpoint();
        await RegisterAsync(client, fresh);
        await RegisterAsync(client, stale);

        // Age the second one past the window. The sweep keys on last success, falling back to when
        // the row was created for a device that never received anything.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.PushSubscriptions
                .Where(s => s.Endpoint == stale)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedDate, DateTime.UtcNow.AddDays(-400)));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var sweep = scope.ServiceProvider
                .GetServices<IRetentionSweep>()
                .Single(s => s.Name == "stale-push-subscriptions");

            var deleted = await sweep.SweepAsync();
            Assert.True(deleted >= 1);
        }

        var endpoints = await EndpointsForAsync(userId);
        Assert.Contains(fresh, endpoints);
        Assert.DoesNotContain(stale, endpoints);
    }

    [Fact]
    public async Task The_stale_sweep_is_registered_alongside_the_refresh_token_sweep()
    {
        using var scope = _factory.Services.CreateScope();

        var names = scope.ServiceProvider.GetServices<IRetentionSweep>().Select(s => s.Name).ToList();

        Assert.Contains("expired-refresh-tokens", names);
        Assert.Contains("stale-push-subscriptions", names);
    }

    [Fact]
    public async Task Deleting_the_account_leaves_no_device_able_to_receive_anything()
    {
        var (client, userId) = await NewUserAsync();
        await RegisterAsync(client, NewEndpoint());
        Assert.NotEmpty(await EndpointsForAsync(userId));

        var deleted = await client.PostAsJsonAsync("/api/v1/account/deletion", new
        {
            password = AuthTestHelpers.ValidPassword,
            confirmation = "DELETE",
        });
        deleted.EnsureSuccessStatusCode();

        Assert.Empty(await EndpointsForAsync(userId));
    }

    // --- helpers ------------------------------------------------------------

    private static string NewEndpoint() => $"https://push.example.com/send/{Guid.NewGuid():N}";

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string endpoint) =>
        client.PostAsJsonAsync("/api/v1/push/subscriptions", new
        {
            endpoint,
            p256dh = "device-public-key",
            auth = "device-auth",
            deviceLabel = "Chrome on Android",
        });

    private static Task<HttpResponseMessage> RemoveAsync(HttpClient client, string endpoint) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push/subscriptions")
        {
            Content = JsonContent.Create(new { endpoint }),
        });

    private async Task<List<string>> EndpointsForAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PushSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.Endpoint)
            .ToListAsync();
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
