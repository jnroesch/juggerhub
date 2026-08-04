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

    /// <summary>An authenticated client (registered + verified + logged in). Browse/search is
    /// authenticated-only since feature 026, so the browse tests view as a signed-in user.</summary>
    public static async Task<HttpClient> AuthedClientAsync(JuggerHubApiFactory factory)
    {
        var (client, _, _, _) = await NewUserAsync(factory);
        return client;
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
                profile.HomeCity = await TestCities.GetOrCreateAsync(db, hometown);
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
                City = city is null ? null : await TestCities.GetOrCreateAsync(db, city),
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
            var cityEntity = city is null ? null : await TestCities.GetOrCreateAsync(db, city);
            var ev = new Event
            {
                Name = name,
                Type = type,
                Description = "Seeded for browse tests.",
                StartsAt = startsAt,
                EndsAt = endsAt,
                LocationKind = city is null ? LocationKind.Virtual : LocationKind.InPerson,
                City = cityEntity,
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

    // ---- Trainings (feature 043) -------------------------------------------------
    //
    // Seeded straight through the DbContext rather than the create API, because several browse
    // invariants have no API path at all: a training predating feature 042 (legacy free-text
    // Location, no CityId), and a session whose address block is set with a deliberately absent
    // venue name. Going through the API would also force a team-admin dance per fixture.

    /// <summary>
    /// Seed a training series (or one-off) with a full structured address. Pass
    /// <paramref name="city"/> = null with a <paramref name="legacyLocation"/> to model a
    /// pre-042 training; pass <paramref name="kind"/> = Virtual for an online training.
    /// </summary>
    public static Task<Guid> SeedTrainingAsync(
        JuggerHubApiFactory factory,
        Guid teamId,
        Guid createdByUserId,
        string name,
        TrainingVisibility visibility = TrainingVisibility.Public,
        string? city = "Berlin",
        string? venueName = null,
        string? street = null,
        string? postalCode = null,
        string? legacyLocation = null,
        LocationKind kind = LocationKind.InPerson,
        bool isRecurring = false) =>
        WithDbAsync(factory, async db =>
        {
            var cityEntity = city is null ? null : await TestCities.GetOrCreateAsync(db, city);
            var training = new Training
            {
                TeamId = teamId,
                CreatedByUserId = createdByUserId,
                Name = name,
                Description = "Seeded for browse tests.",
                LocationKind = kind,
                VenueName = kind == LocationKind.Virtual ? null : venueName,
                Street = kind == LocationKind.Virtual ? null : street,
                PostalCode = kind == LocationKind.Virtual ? null : postalCode,
                City = kind == LocationKind.Virtual ? null : cityEntity,
                // The system-derived legacy label (042). Mirrors what the service writes: "City,
                // Country" when in-person, null when virtual — unless a test supplies its own to
                // model a pre-042 row.
                Location = kind == LocationKind.Virtual
                    ? null
                    : legacyLocation ?? (cityEntity is null ? null : $"{cityEntity.Name}, {cityEntity.CountryName}"),
                VirtualLink = kind == LocationKind.Virtual ? "https://example.com/meet" : null,
                IsRecurring = isRecurring,
                Weekday = isRecurring ? DayOfWeek.Tuesday : null,
                Interval = isRecurring ? TrainingInterval.Weekly : null,
                StartTime = new TimeOnly(19, 0),
                EndTime = new TimeOnly(21, 0),
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = isRecurring ? DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6) : null,
                Visibility = visibility,
            };
            db.Trainings.Add(training);
            await db.SaveChangesAsync();
            return training.Id;
        });

    /// <summary>
    /// Seed one dated session of a training.
    /// </summary>
    /// <remarks>
    /// ⚠ The address override is an INDIVISIBLE BLOCK keyed on <c>CityIdOverride</c> (feature 042).
    /// Supplying <paramref name="overrideCity"/> is what detaches the whole block — the other three
    /// override columns are then authoritative <em>including when they are null</em>, which is
    /// exactly the case the venue-leak guard needs (a relocated session at a venue-less address).
    /// </remarks>
    public static Task<Guid> SeedTrainingSessionAsync(
        JuggerHubApiFactory factory,
        Guid trainingId,
        DateOnly sessionDate,
        TrainingVisibility? visibilityOverride = null,
        TrainingSessionStatus status = TrainingSessionStatus.Scheduled,
        string? overrideCity = null,
        string? overrideVenueName = null,
        string? overrideStreet = null,
        string? overridePostalCode = null,
        LocationKind? kindOverride = null) =>
        WithDbAsync(factory, async db =>
        {
            var training = await db.Trainings.AsNoTracking()
                .Where(t => t.Id == trainingId)
                .Select(t => new { t.TeamId })
                .FirstAsync();

            var overrideCityEntity = overrideCity is null ? null : await TestCities.GetOrCreateAsync(db, overrideCity);
            var session = new TrainingSession
            {
                TrainingId = trainingId,
                TeamId = training.TeamId,
                SessionDate = sessionDate,
                VisibilityOverride = visibilityOverride,
                Status = status,
                LocationKindOverride = kindOverride,
                CityOverride = overrideCityEntity,
                VenueNameOverride = overrideVenueName,
                StreetOverride = overrideStreet,
                PostalCodeOverride = overridePostalCode,
                LocationOverride = overrideCityEntity is null
                    ? null
                    : $"{overrideCityEntity.Name}, {overrideCityEntity.CountryName}",
                Detached = overrideCityEntity is not null,
            };
            db.TrainingSessions.Add(session);
            await db.SaveChangesAsync();
            return session.Id;
        });

    /// <summary>Set the caller's home city — the anchor for the nearest-first ordering.</summary>
    public static Task SetHomeCityAsync(JuggerHubApiFactory factory, Guid userId, string city) =>
        WithDbAsync(factory, async db =>
        {
            var profile = await db.PlayerProfiles.FirstAsync(p => p.UserId == userId);
            profile.HomeCity = await TestCities.GetOrCreateAsync(db, city);
            await db.SaveChangesAsync();
        });

    /// <summary>Add a membership so a test can prove browse does NOT widen for members.</summary>
    public static Task AddTeamMemberAsync(
        JuggerHubApiFactory factory, Guid teamId, Guid userId, TeamRole role = TeamRole.Member) =>
        WithDbAsync(factory, async db =>
        {
            db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = teamId,
                UserId = userId,
                Role = role,
                JoinedDate = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

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
