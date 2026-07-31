using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Security.PlatformAdmin;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

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
            new { name = "Test Team", slug, type = "CityTeam", location = new { cityExternalId = "TEST:berlin" } });
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

    /// <summary>
    /// A small, genuinely decodable PNG for icon-upload tests. Synthesized rather than a hard-coded
    /// blob: since #101 every icon is decoded, so bytes that merely carry the right magic numbers
    /// — which the old sniff-only validation accepted — are now (correctly) rejected.
    /// </summary>
    public static byte[] TinyPng()
    {
        using var img = new Image<Rgba32>(8, 8, new Rgba32(10, 120, 200));
        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>
    /// A 900x600 photo-like PNG: non-square (so the Fit profile's downscale is observable) and
    /// continuous-tone on purpose — PNG squeezes synthetic high-frequency noise into a few KB that
    /// lossy WebP cannot beat, which would break a "smaller after normalization" comparison for
    /// reasons unrelated to the pipeline.
    /// </summary>
    public static byte[] LargePng(out int length)
    {
        using var img = new Image<Rgba32>(900, 600);
        for (var y = 0; y < img.Height; y++)
        {
            for (var x = 0; x < img.Width; x++)
            {
                img[x, y] = new Rgba32(
                    (byte)(128 + (127 * Math.Sin(x * 0.01))),
                    (byte)(128 + (127 * Math.Sin(y * 0.013))),
                    (byte)(128 + (127 * Math.Sin((x + y) * 0.007))));
            }
        }

        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        var bytes = ms.ToArray();
        length = bytes.Length;
        return bytes;
    }
}
