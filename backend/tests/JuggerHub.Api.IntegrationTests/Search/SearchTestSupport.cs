using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>Shares one Testcontainers Postgres + host across all search/browse test classes.</summary>
[CollectionDefinition("Search")]
public sealed class SearchCollection : ICollectionFixture<JuggerHubApiFactory>;

/// <summary>
/// Helpers for the browse/search tests (feature 007). Registers real players via the API
/// (so an Identity user + profile exist) then seeds teams, events, participations, and the two
/// new flags directly through the DbContext for precise control over browse inputs.
/// </summary>
internal static class SearchTestSupport
{
    public static async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync(
        JuggerHubApiFactory factory)
    {
        var client = factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, factory, handle: handle);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }

    public static async Task<T> WithDbAsync<T>(JuggerHubApiFactory factory, Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    public static Task WithDbAsync(JuggerHubApiFactory factory, Func<AppDbContext, Task> action) =>
        WithDbAsync(factory, async db =>
        {
            await action(db);
            return true;
        });

    public static Task<Guid> ProfileIdAsync(JuggerHubApiFactory factory, Guid userId) =>
        WithDbAsync(factory, db => db.PlayerProfiles.Where(p => p.UserId == userId).Select(p => p.Id).FirstAsync());

    /// <summary>Set a player's optional display name/hometown, and add declared pompfen.</summary>
    public static Task ConfigurePlayerAsync(
        JuggerHubApiFactory factory, Guid userId,
        string? displayName = null, string? hometown = null, params Pompfe[] pompfen) =>
        WithDbAsync(factory, async db =>
        {
            var profile = await db.PlayerProfiles.Include(p => p.Pompfen).FirstAsync(p => p.UserId == userId);
            if (displayName is not null)
            {
                profile.DisplayName = displayName;
            }

            if (hometown is not null)
            {
                profile.Hometown = hometown;
            }

            foreach (var pompfe in pompfen)
            {
                if (profile.Pompfen.All(pp => pp.Pompfe != pompfe))
                {
                    db.ProfilePompfen.Add(new ProfilePompfe { ProfileId = profile.Id, Pompfe = pompfe });
                }
            }

            await db.SaveChangesAsync();
        });

    /// <summary>Seed a team directly (bypasses the create API so we control slug/city/flags).</summary>
    public static Task<(Guid Id, string Slug)> SeedTeamAsync(
        JuggerHubApiFactory factory, string name, string? city, bool beginnersWelcome = false)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        return WithDbAsync(factory, async db =>
        {
            var team = new Team
            {
                Slug = slug,
                Name = name,
                Type = city is null ? TeamType.Mixteam : TeamType.CityTeam,
                City = city,
                BeginnersWelcome = beginnersWelcome,
            };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
            return (team.Id, team.Slug);
        });
    }

    /// <summary>Seed a published or cancelled event with a schedule and city.</summary>
    public static Task<Guid> SeedEventAsync(
        JuggerHubApiFactory factory,
        string name,
        DateTime startsAt,
        DateTime endsAt,
        EventType type = EventType.Tournament,
        string? city = "Berlin",
        EventStatus status = EventStatus.Published)
    {
        return WithDbAsync(factory, async db =>
        {
            var ev = new Event
            {
                Name = name,
                Type = type,
                Description = "Seeded for browse tests.",
                StartsAt = startsAt,
                EndsAt = endsAt,
                LocationKind = city is null ? LocationKind.Virtual : LocationKind.InPerson,
                City = city,
                Country = city is null ? null : "Germany",
                VirtualLink = city is null ? "https://example.com/meet" : null,
                Location = city ?? "Online",
                ParticipantMode = ParticipantMode.Individuals,
                ParticipationLimit = 32,
                Status = status,
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            return ev.Id;
        });
    }

    /// <summary>Backdate a team's creation timestamp (to test the created-within-12-months active rule).</summary>
    public static Task BackdateTeamCreatedAsync(JuggerHubApiFactory factory, Guid teamId, DateTime created) =>
        WithDbAsync(factory, async db =>
        {
            await db.Teams.Where(t => t.Id == teamId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedDate, created));
        });

    /// <summary>Attribute a player's participation in an event to a team (drives team "active").</summary>
    public static Task AddParticipationAsync(
        JuggerHubApiFactory factory, Guid profileId, Guid eventId, Guid teamId, string teamLabel) =>
        WithDbAsync(factory, async db =>
        {
            db.EventParticipations.Add(new EventParticipation
            {
                ProfileId = profileId,
                EventId = eventId,
                TeamId = teamId,
                TeamLabel = teamLabel,
            });
            await db.SaveChangesAsync();
        });
}
