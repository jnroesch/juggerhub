using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.AccountDeletion;

/// <summary>One Testcontainers Postgres + host shared by the feature-037 erasure suites.</summary>
[CollectionDefinition("AccountDeletion")]
public sealed class AccountDeletionCollection : ICollectionFixture<JuggerHubApiFactory>;

/// <summary>
/// Shared helpers for the account-erasure tests: seeding a member with something worth erasing, and
/// the two endpoint calls.
/// </summary>
public abstract class AccountDeletionTestSupport
{
    protected JuggerHubApiFactory Factory { get; }

    protected AccountDeletionTestSupport(JuggerHubApiFactory factory) => Factory = factory;

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The English confirmation literal. The server accepts the en/de/es set (T064).</summary>
    protected const string Confirm = "DELETE";

    protected async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewMemberAsync()
    {
        var client = Factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, Factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }

    protected static Task<HttpResponseMessage> DeleteAccountAsync(
        HttpClient client, string password = AuthTestHelpers.ValidPassword, string confirmation = Confirm) =>
        client.PostAsJsonAsync("/api/v1/account/deletion", new { password, confirmation });

    protected static Task<HttpResponseMessage> PreviewAsync(HttpClient client) =>
        client.GetAsync("/api/v1/account/deletion-preview");

    protected async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected async Task WithDbAsync(Func<AppDbContext, Task> work)
    {
        using var scope = Factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected Task SetStatusAsync(Guid userId, AccountStatus status) =>
        WithDbAsync(db => db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, status)));

    /// <summary>A team with the given member as its sole admin.</summary>
    protected async Task<Guid> CreateTeamWithSoleAdminAsync(Guid adminUserId, string name = "Rheinfeuer")
    {
        return await WithDbAsync(async db =>
        {
            var team = new Team
            {
                Name = name,
                Slug = "t" + Guid.NewGuid().ToString("N")[..12],
                Type = TeamType.CityTeam,
            };
            db.Teams.Add(team);
            db.TeamMemberships.Add(new TeamMembership
            {
                Team = team,
                UserId = adminUserId,
                Role = TeamRole.Admin,
                JoinedDate = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return team.Id;
        });
    }

    protected Task AddTeamAdminAsync(Guid teamId, Guid userId) =>
        WithDbAsync(async db =>
        {
            db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = teamId,
                UserId = userId,
                Role = TeamRole.Admin,
                JoinedDate = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    protected Task<AccountStatus?> StatusAsync(Guid userId) =>
        WithDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (AccountStatus?)u.Status)
            .FirstOrDefaultAsync());

    protected Task<bool> HasProfileAsync(Guid userId) =>
        WithDbAsync(db => db.PlayerProfiles.IgnoreQueryFilters().AnyAsync(p => p.UserId == userId));
}
