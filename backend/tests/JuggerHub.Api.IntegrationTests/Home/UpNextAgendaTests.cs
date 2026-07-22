using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Home;

/// <summary>
/// Integration tests for the unified "Up next" agenda (feature 025, US2): events and trainings folded
/// into one soonest-first list, multi-team event de-dup, near-window un-answered trainings excluded
/// (they belong to "Needs you"), and unrelated events absent.
/// </summary>
[Collection("Home")]
public sealed class UpNextAgendaTests
{
    private static readonly DateTime Soon = DateTime.UtcNow.AddDays(3);

    private readonly JuggerHubApiFactory _factory;

    public UpNextAgendaTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_far_out_training_session_folds_into_up_next_as_a_training_item()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Drillers");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);

        // 20 days out ⇒ beyond the ~14-day near window ⇒ stays in Up next even without an answer.
        var farDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20));
        var sessionId = await SeedTrainingSessionAsync(teamId, userId, "Sunday drills", farDate);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var item = home.GetProperty("upNext").EnumerateArray()
            .First(i => i.GetProperty("kind").GetString() == "Training" && i.GetProperty("id").GetString() == sessionId.ToString());
        Assert.Equal("Sunday drills", item.GetProperty("title").GetString());
    }

    [Fact]
    public async Task An_unanswered_training_appears_in_up_next_not_in_needs_you()
    {
        // Feature 025 revision: trainings never surface in "Needs you" (invites/requests only) — every
        // upcoming session, answered or not, lives in "Up next" with an inline RSVP.
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Sprinters");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);

        var nearDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var sessionId = await SeedTrainingSessionAsync(teamId, userId, "Tue drills", nearDate);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");

        Assert.Contains(home.GetProperty("upNext").EnumerateArray(),
            i => i.GetProperty("kind").GetString() == "Training" && i.GetProperty("id").GetString() == sessionId.ToString());
        Assert.Empty(home.GetProperty("needsYou").EnumerateArray());
    }

    [Fact]
    public async Task A_multi_team_viewer_sees_a_shared_team_event_once()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamA, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Team A");
        var (teamB, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Team B");
        await HomeTestSupport.AddMemberAsync(_factory, teamA, userId);
        await HomeTestSupport.AddMemberAsync(_factory, teamB, userId);

        var ev = await HomeTestSupport.SeedEventAsync(_factory, "Shared match", Soon, Soon.AddHours(2), ParticipantMode.Teams);
        await HomeTestSupport.SignupTeamAsync(_factory, ev, teamA);
        await HomeTestSupport.SignupTeamAsync(_factory, ev, teamB);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var matches = home.GetProperty("upNext").EnumerateArray()
            .Count(i => i.GetProperty("title").GetString() == "Shared match");
        Assert.Equal(1, matches);
    }

    [Fact]
    public async Task An_event_the_viewer_is_not_part_of_does_not_appear()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Mine");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);

        await HomeTestSupport.SeedEventAsync(_factory, "Someone else's tournament", Soon, Soon.AddDays(1), ParticipantMode.Teams, type: EventType.Tournament);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.DoesNotContain(home.GetProperty("upNext").EnumerateArray(),
            i => i.GetProperty("title").GetString() == "Someone else's tournament");
    }

    private Task<Guid> SeedTrainingSessionAsync(Guid teamId, Guid createdByUserId, string name, DateOnly date) =>
        HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            var training = new Training
            {
                TeamId = teamId,
                Name = name,
                LocationKind = LocationKind.InPerson,
                Location = "Halle Süd",
                IsRecurring = false,
                StartTime = new TimeOnly(19, 0),
                EndTime = new TimeOnly(21, 0),
                StartDate = date,
                Visibility = TrainingVisibility.TeamOnly,
                CreatedByUserId = createdByUserId,
            };
            db.Trainings.Add(training);
            await db.SaveChangesAsync();

            var session = new TrainingSession
            {
                TrainingId = training.Id,
                TeamId = teamId,
                SessionDate = date,
                Status = TrainingSessionStatus.Scheduled,
            };
            db.TrainingSessions.Add(session);
            await db.SaveChangesAsync();
            return session.Id;
        });
}
