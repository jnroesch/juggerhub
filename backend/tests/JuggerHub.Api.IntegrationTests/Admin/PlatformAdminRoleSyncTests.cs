using System.Net;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Security.PlatformAdmin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
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
    private WebApplicationFactoryFixture DerivedHost(string emails) => new(_factory, emails);

    private sealed class WebApplicationFactoryFixture : IDisposable
    {
        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host { get; }

        public WebApplicationFactoryFixture(JuggerHubApiFactory parent, string emails)
        {
            Host = parent.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Admin:Emails"] = emails,
                    })));
        }

        public void Dispose() => Host.Dispose();
    }

    [Fact]
    public async Task Role_sync_lifecycle_grant_idempotence_revoke_fail_closed_regrant()
    {
        // -- Register the configured admin identity AFTER "startup": like production,
        //    nothing is granted yet (the startup sync ran before the account existed).
        var setup = _factory.CreateClient();
        await AuthTestHelpers.RegisterAndVerifyAsync(setup, _factory, email: AdminEmail);

        var admin = _factory.CreateClient();
        (await AuthTestHelpers.LoginAsync(admin, AdminEmail, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();

        // SC-001: presence in the configuration alone grants NOTHING per-request.
        var beforeSync = await admin.GetAsync("/api/v1/admin/access");
        Assert.Equal(HttpStatusCode.Forbidden, beforeSync.StatusCode);

        // -- "Next startup": the sync designates the now-existing account.
        await RunSyncAsync(_factory.Services);
        var afterSync = await admin.GetAsync("/api/v1/admin/access");
        Assert.Equal(HttpStatusCode.OK, afterSync.StatusCode);

        // -- Idempotence: re-running changes nothing and logs no errors.
        var errorsBefore = _factory.ErrorLogs.Count;
        await RunSyncAsync(_factory.Services);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/access")).StatusCode);
        Assert.Equal(errorsBefore, _factory.ErrorLogs.Count);

        // -- A configured-but-unregistered email is skipped without error, existing admin intact.
        using (var withGhost = DerivedHost($"{AdminEmail},ghost@test.de"))
        {
            await RunSyncAsync(withGhost.Host.Services);
        }

        Assert.Equal(errorsBefore, _factory.ErrorLogs.Count);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/access")).StatusCode);

        // -- Mirror semantics: removal from configuration REVOKES at the next sync,
        //    and with zero admins everything fails closed (403 for the ex-admin).
        using (var emptied = DerivedHost(string.Empty))
        {
            await RunSyncAsync(emptied.Host.Services);
        }

        var revoked = await admin.GetAsync("/api/v1/admin/access");
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);

        // Anonymous stays 401 regardless.
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/admin/access")).StatusCode);

        // -- Re-adding the identity re-grants at the next sync (round trip complete).
        await RunSyncAsync(_factory.Services);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/access")).StatusCode);
    }
}
