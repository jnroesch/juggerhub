using System.Net;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Security.PlatformAdmin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Admin;

/// <summary>
/// Feature 013 (US1, GitHub issue #21) — the config-mirrored <c>PlatformAdmin</c> role.
/// Uses its OWN factory (class fixture, dedicated database) because these tests
/// deliberately grant and revoke the role by re-running the startup sync with
/// different configurations; sharing state with other suites would be racy.
/// The scenario runs as one sequential lifecycle so every step's precondition is explicit.
/// </summary>
public sealed class PlatformAdminRoleSyncTests : IClassFixture<JuggerHubApiFactory>
{
    private const string AdminEmail = "admin@test.de";

    private readonly JuggerHubApiFactory _factory;

    public PlatformAdminRoleSyncTests(JuggerHubApiFactory factory) => _factory = factory;

    private static async Task RunSyncAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PlatformAdminRoleSync>().SyncAsync();
    }

    /// <summary>A derived host over the SAME database with a different admin configuration.</summary>
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> DerivedHost(string emails) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Admin:Emails"] = emails,
                })));

    [Fact]
    public async Task Role_lifecycle_registration_grant_sync_pickup_revoke_fail_closed_regrant()
    {
        // -- A CONFIGURED identity registering is designated immediately (no restart).
        var setup = _factory.CreateClient();
        await AuthTestHelpers.RegisterAndVerifyAsync(setup, _factory, email: AdminEmail);

        var admin = _factory.CreateClient();
        (await AuthTestHelpers.LoginAsync(admin, AdminEmail, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/access")).StatusCode);

        // -- Idempotence: re-running the sync changes nothing and logs no errors.
        var errorsBefore = _factory.ErrorLogs.Count;
        await RunSyncAsync(_factory.Services);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/access")).StatusCode);
        Assert.Equal(errorsBefore, _factory.ErrorLogs.Count);

        // -- A configured-but-unregistered email is skipped without error, existing admin intact.
        using (var withGhost = DerivedHost($"{AdminEmail},ghost@test.de"))
        {
            await RunSyncAsync(withGhost.Services);
        }

        Assert.Equal(errorsBefore, _factory.ErrorLogs.Count);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/access")).StatusCode);

        // -- SC-001: config alone grants NOTHING per-request. An account that pre-exists a
        //    config addition stays refused until a sync ("next startup") applies it.
        var (userClient, _, _, extraEmail) = await NewUserAsync();
        using (var withExtra = DerivedHost($"{AdminEmail},{extraEmail}"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await userClient.GetAsync("/api/v1/admin/access")).StatusCode);

            // The "next startup" of that configuration designates them.
            await RunSyncAsync(withExtra.Services);
            Assert.Equal(HttpStatusCode.OK, (await userClient.GetAsync("/api/v1/admin/access")).StatusCode);
        }

        // -- Mirror semantics: removal from configuration REVOKES at the next sync,
        //    and with zero admins everything fails closed (403 for every ex-admin).
        using (var emptied = DerivedHost(string.Empty))
        {
            await RunSyncAsync(emptied.Services);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/access")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.GetAsync("/api/v1/admin/access")).StatusCode);

        // Anonymous stays 401 regardless.
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/admin/access")).StatusCode);

        // -- Re-adding the identity re-grants at the next sync (round trip complete).
        await RunSyncAsync(_factory.Services);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/access")).StatusCode);
    }

    private async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }
}
