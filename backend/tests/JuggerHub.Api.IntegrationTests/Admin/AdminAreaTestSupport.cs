using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Recognition;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Admin;

/// <summary>
/// One shared Testcontainers Postgres + host for the feature-013 admin-area suites
/// (overview, users list, account actions, enforcement). Kept SEPARATE from the
/// "Recognition" collection so account-state mutations (suspend/ban) never race the
/// 012 suites, while the role-sync lifecycle tests use their own per-class factory.
/// </summary>
[CollectionDefinition("AdminArea")]
public sealed class AdminAreaCollection : ICollectionFixture<JuggerHubApiFactory>;

internal static class AdminAreaTestSupport
{
    private static readonly SemaphoreSlim AdminGate = new(1, 1);
    private static bool _adminReady;

    /// <summary>An authenticated client for the configured platform admin (role synced).</summary>
    public static async Task<(HttpClient Client, Guid UserId)> AdminClientAsync(JuggerHubApiFactory factory)
    {
        Guid adminId;
        await AdminGate.WaitAsync();
        try
        {
            if (!_adminReady)
            {
                var setup = factory.CreateClient();
                await AuthTestHelpers.RegisterAndVerifyAsync(setup, factory, email: RecognitionTestSupport.AdminEmail);
                await RecognitionTestSupport.RunAdminRoleSyncAsync(factory);
                _adminReady = true;
            }

            adminId = await WithDbAsync(factory, db =>
                db.Users.Where(u => u.Email == RecognitionTestSupport.AdminEmail).Select(u => u.Id).SingleAsync());
        }
        finally
        {
            AdminGate.Release();
        }

        var client = factory.CreateClient();
        (await AuthTestHelpers.LoginAsync(client, RecognitionTestSupport.AdminEmail, AuthTestHelpers.ValidPassword))
            .EnsureSuccessStatusCode();
        return (client, adminId);
    }

    /// <summary>A verified, signed-in regular player with a fresh handle.</summary>
    public static async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> PlayerClientAsync(
        JuggerHubApiFactory factory)
    {
        var client = factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }

    /// <summary>Runs a scoped operation against the real database (test fixture setup/asserts).</summary>
    public static async Task<T> WithDbAsync<T>(JuggerHubApiFactory factory, Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public static async Task WithDbAsync(JuggerHubApiFactory factory, Func<AppDbContext, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Sets an account's state directly (fixture setup, not the API under test).</summary>
    public static Task SetStatusAsync(JuggerHubApiFactory factory, Guid userId, AccountStatus status) =>
        WithDbAsync(factory, async db =>
        {
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.Status, status)
                    .SetProperty(u => u.StatusChangedAt, DateTime.UtcNow));
        });
}
