using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Security.PlatformAdmin;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Recognition;

/// <summary>One shared Testcontainers Postgres + host across all badge/achievement test classes.</summary>
[CollectionDefinition("Recognition")]
public sealed class RecognitionCollection : ICollectionFixture<JuggerHubApiFactory>;

internal static class RecognitionTestSupport
{
    /// <summary>Matches the factory's configured admin identities (<c>Admin:Emails</c>).</summary>
    public const string AdminEmail = "admin@test.de";

    private static readonly SemaphoreSlim AdminGate = new(1, 1);
    private static bool _adminReady;

    /// <summary>
    /// Re-runs the feature-013 startup role sync. The configured admin registers AFTER the test
    /// host booted, so — exactly like production — the account is picked up "at the next
    /// startup", which this simulates by invoking the real sync again.
    /// </summary>
    public static async Task RunAdminRoleSyncAsync(JuggerHubApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PlatformAdminRoleSync>().SyncAsync();
    }

    /// <summary>An authenticated HttpClient for the platform admin (registered once per run).</summary>
    public static async Task<HttpClient> AdminClientAsync(JuggerHubApiFactory factory)
    {
        await AdminGate.WaitAsync();
        try
        {
            if (!_adminReady)
            {
                var setup = factory.CreateClient();
                await AuthTestHelpers.RegisterAndVerifyAsync(setup, factory, email: AdminEmail);
                // Feature 013: registration alone no longer grants anything — the role
                // sync must run (as it would at the next startup) to designate the admin.
                await RunAdminRoleSyncAsync(factory);
                _adminReady = true;
            }
        }
        finally
        {
            AdminGate.Release();
        }

        var client = factory.CreateClient();
        (await AuthTestHelpers.LoginAsync(client, AdminEmail, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>A verified, signed-in non-admin user with a fresh profile handle.</summary>
    public static async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> UserClientAsync(JuggerHubApiFactory factory)
    {
        var client = factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }

    /// <summary>Creates a team owned by <paramref name="client"/> and returns its slug.</summary>
    public static async Task<string> CreateTeamAsync(HttpClient client, string? slug = null)
    {
        slug ??= $"t{Guid.NewGuid():N}"[..18];
        var resp = await client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Test Team", slug, type = "CityTeam", city = "Berlin" });
        resp.EnsureSuccessStatusCode();
        return slug;
    }

    /// <summary>Create a badge/achievement definition as admin; returns its id.</summary>
    public static async Task<Guid> CreateDefinitionAsync(
        HttpClient admin, string resource, bool appliesToPlayers = true, bool appliesToTeams = true, string? name = null)
    {
        var resp = await admin.PostAsJsonAsync($"/api/v1/admin/{resource}", new
        {
            name = name ?? $"Def {Guid.NewGuid():N}"[..12],
            description = "A test recognition.",
            appliesToPlayers,
            appliesToTeams,
        });
        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return dto.GetProperty("id").GetGuid();
    }

    /// <summary>A 1x1 PNG (valid magic bytes) for icon-upload tests.</summary>
    public static byte[] TinyPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
