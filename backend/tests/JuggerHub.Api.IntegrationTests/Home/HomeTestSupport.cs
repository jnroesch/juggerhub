using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Home;

/// <summary>Shares one Testcontainers Postgres + host across the Home dashboard test classes.</summary>
[CollectionDefinition("Home")]
public sealed class HomeCollection : ICollectionFixture<JuggerHubApiFactory>;

/// <summary>
/// Helpers for the Home dashboard tests (feature 008). Registers real players via the API,
/// then seeds teams, memberships, events, sign-ups, and news directly through the DbContext
/// for precise control over what each viewer is entitled to see.
/// </summary>
internal static class HomeTestSupport
{
    public static async Task<(HttpClient Client, Guid UserId)> NewUserAsync(JuggerHubApiFactory factory)
    {
        var client = factory.CreateClient();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, factory, handle: AuthTestHelpers.NewHandle());
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId);
    }

    public static async Task<T> WithDbAsync<T>(JuggerHubApiFactory factory, Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    public static Task WithDbAsync(JuggerHubApiFactory factory, Func<AppDbContext, Task> action) =>
        WithDbAsync(factory, async db => { await action(db); return true; });

    public static Task<(Guid Id, string Slug)> SeedTeamAsync(JuggerHubApiFactory factory, string name) =>
        WithDbAsync(factory, async db =>
        {
            var team = new Team { Slug = "t" + Guid.NewGuid().ToString("N")[..12], Name = name, Type = TeamType.Mixteam };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
            return (team.Id, team.Slug);
        });

    public static Task AddMemberAsync(JuggerHubApiFactory factory, Guid teamId, Guid userId, TeamRole role = TeamRole.Member) =>
        WithDbAsync(factory, async db =>
        {
            db.TeamMemberships.Add(new TeamMembership { TeamId = teamId, UserId = userId, Role = role, JoinedDate = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

    public static Task<Guid> SeedEventAsync(
        JuggerHubApiFactory factory, string name, DateTime startsAt, DateTime endsAt,
        ParticipantMode mode, EventType type = EventType.Workshop,
        EventStatus status = EventStatus.Published, int limit = 20, string? city = "Berlin") =>
        WithDbAsync(factory, async db =>
        {
            var ev = new Event
            {
                Name = name,
                Type = type,
                Description = "Seeded for Home tests.",
                StartsAt = startsAt,
                EndsAt = endsAt,
                LocationKind = city is null ? LocationKind.Virtual : LocationKind.InPerson,
                City = city,
                Country = city is null ? null : "Germany",
                VirtualLink = city is null ? "https://example.com/meet" : null,
                Location = city ?? "Online",
                ParticipantMode = mode,
                ParticipationLimit = limit,
                Status = status,
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            return ev.Id;
        });

    public static Task<Guid> SignupUserAsync(JuggerHubApiFactory factory, Guid eventId, Guid userId, SignupStatus status = SignupStatus.Joined) =>
        WithDbAsync(factory, async db =>
        {
            var s = new EventSignup { EventId = eventId, UserId = userId, Status = status };
            db.EventSignups.Add(s);
            await db.SaveChangesAsync();
            return s.Id;
        });

    public static Task SignupTeamAsync(JuggerHubApiFactory factory, Guid eventId, Guid teamId, SignupStatus status = SignupStatus.Joined) =>
        WithDbAsync(factory, async db =>
        {
            db.EventSignups.Add(new EventSignup { EventId = eventId, TeamId = teamId, Status = status });
            await db.SaveChangesAsync();
        });

    public static Task AddTeamNewsAsync(JuggerHubApiFactory factory, Guid teamId, Guid authorUserId, string body) =>
        WithDbAsync(factory, async db =>
        {
            db.TeamNewsPosts.Add(new TeamNewsPost { TeamId = teamId, AuthorUserId = authorUserId, Body = body });
            await db.SaveChangesAsync();
        });

    public static Task AddEventNewsAsync(JuggerHubApiFactory factory, Guid eventId, Guid authorUserId, string body) =>
        WithDbAsync(factory, async db =>
        {
            db.EventNewsPosts.Add(new EventNewsPost { EventId = eventId, AuthorUserId = authorUserId, Body = body });
            await db.SaveChangesAsync();
        });
}
