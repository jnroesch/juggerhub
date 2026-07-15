using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Trainings;

/// <summary>Shares one Testcontainers Postgres + host across all training test classes.</summary>
[CollectionDefinition("Trainings")]
public sealed class TrainingsCollection : ICollectionFixture<JuggerHubApiFactory>;

/// <summary>
/// Shared helpers for the trainings (018) integration tests: user/team/training seeding against the real
/// API + Postgres container.
/// </summary>
public abstract class TrainingTestSupport
{
    protected JuggerHubApiFactory Factory { get; }

    protected TrainingTestSupport(JuggerHubApiFactory factory) => Factory = factory;

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected async Task<(HttpClient Client, Guid UserId, string Handle)> NewUserAsync()
    {
        var client = Factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, Factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle);
    }

    protected async Task<(Guid TeamId, string Slug)> CreateTeamAsync(HttpClient adminClient)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var resp = await adminClient.PostAsJsonAsync("/api/v1/teams",
            new { name = "Rheinfeuer", slug, type = "CityTeam", city = "Köln" });
        Assert.True(resp.IsSuccessStatusCode, $"create team failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teamId = await db.Teams.Where(t => t.Slug == slug).Select(t => t.Id).FirstAsync();
        return (teamId, slug);
    }

    protected async Task AddTeamMemberAsync(Guid teamId, Guid userId, TeamRole role = TeamRole.Member)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TeamMemberships.Add(new TeamMembership { TeamId = teamId, UserId = userId, Role = role, JoinedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    /// <summary>The next occurrence (strictly future) of <paramref name="weekday"/> as a date-only.</summary>
    protected static DateOnly NextWeekday(DayOfWeek weekday, int weeksAhead = 0)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var delta = ((int)weekday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(delta + 7 * weeksAhead);
    }

    protected async Task<JsonElement> CreateSeriesAsync(
        HttpClient client, string slug, DayOfWeek weekday, DateOnly start, DateOnly end,
        string interval = "Weekly", string visibility = "TeamOnly", string name = "Tuesday Training")
    {
        var body = new
        {
            isRecurring = true,
            name,
            description = "Drills then scrims.",
            locationKind = "InPerson",
            location = "Sportpark Müngersdorf, Köln",
            virtualLink = (string?)null,
            weekday = weekday.ToString(),
            interval,
            startTime = "19:00:00",
            endTime = "21:00:00",
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            visibility,
        };
        var resp = await client.PostAsJsonAsync($"/api/v1/teams/{slug}/trainings", body);
        var payload = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, $"create series failed: {(int)resp.StatusCode} {payload}");
        return JsonSerializer.Deserialize<JsonElement>(payload);
    }

    protected async Task<JsonElement> CreateOneOffAsync(HttpClient client, string slug, DateOnly date, string visibility = "TeamOnly")
    {
        var body = new
        {
            isRecurring = false,
            name = "Extra scrimmage",
            description = (string?)null,
            locationKind = "InPerson",
            location = "Tempelhofer Feld",
            virtualLink = (string?)null,
            weekday = (string?)null,
            interval = (string?)null,
            startTime = "14:00:00",
            endTime = "16:00:00",
            startDate = date.ToString("yyyy-MM-dd"),
            endDate = (string?)null,
            visibility,
        };
        var resp = await client.PostAsJsonAsync($"/api/v1/teams/{slug}/trainings", body);
        var payload = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, $"create one-off failed: {(int)resp.StatusCode} {payload}");
        return JsonSerializer.Deserialize<JsonElement>(payload);
    }

    protected async Task<int> SessionCountAsync(Guid trainingId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TrainingSessions.CountAsync(s => s.TrainingId == trainingId);
    }
}
