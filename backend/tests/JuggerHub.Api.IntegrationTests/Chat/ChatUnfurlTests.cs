using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Link unfurl (feature 019, User Story 7).
/// </summary>
/// <remarks>
/// <see cref="The_same_message_shows_a_card_to_a_member_and_a_plain_link_to_an_outsider"/> is SC-007
/// and the reason cards are resolved per <em>viewer</em> at read time rather than snapshotted at send.
/// </remarks>
[Collection("Chat")]
public sealed class ChatUnfurlTests : ChatTestSupport
{
    public ChatUnfurlTests(JuggerHubApiFactory factory) : base(factory) { }

    /// <summary>Seed a training session directly; the training API's own flow is feature 018's business.</summary>
    private async Task<Guid> SeedTrainingSessionAsync(Guid teamId, Guid creatorId, TrainingVisibility visibility, string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var training = new Training
        {
            TeamId = teamId,
            Name = name,
            LocationKind = LocationKind.InPerson,
            Location = "Sportpark",
            IsRecurring = false,
            StartTime = new TimeOnly(19, 0),
            EndTime = new TimeOnly(21, 0),
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Visibility = visibility,
            CreatedByUserId = creatorId,
        };
        db.Trainings.Add(training);

        var session = new TrainingSession
        {
            TrainingId = training.Id,
            TeamId = teamId,
            SessionDate = training.StartDate,
            Status = TrainingSessionStatus.Scheduled,
        };
        db.TrainingSessions.Add(session);

        await db.SaveChangesAsync();
        return session.Id;
    }

    private static JsonElement Card(JsonElement page) => page.GetProperty("items")[0].GetProperty("linkCard");

    [Fact]
    public async Task A_team_link_unfurls_into_a_view_only_card()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(ada);
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, $"you should scrim them http://localhost:4200/t/{slug}");

        var card = Card(await GetMessagesAsync(ben, conversationId));
        Assert.Equal("Team", card.GetProperty("kind").GetString());
        Assert.Equal("Rheinfeuer", card.GetProperty("title").GetString());
        Assert.Equal($"/t/{slug}", card.GetProperty("href").GetString());
        Assert.Equal(teamId, card.GetProperty("targetId").GetGuid());

        // FR-038: view-only. The card carries a link and nothing to act on — no RSVP, no join.
        // (The deliberate scope cut from wireframe 9c.)
        Assert.False(card.TryGetProperty("actions", out _));
    }

    [Fact]
    public async Task A_player_link_unfurls()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, benHandle) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, $"that's him /u/{benHandle}");

        var card = Card(await GetMessagesAsync(ben, conversationId));
        Assert.Equal("Player", card.GetProperty("kind").GetString());
        Assert.Equal($"/u/{benHandle}", card.GetProperty("href").GetString());
    }

    [Fact]
    public async Task A_public_training_link_unfurls_for_anyone()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (zoe, zoeId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        var sessionId = await SeedTrainingSessionAsync(teamId, adaId, TrainingVisibility.Public, "Open mat");
        var conversationId = await StartDirectAsync(ada, zoeId);

        await SendAsync(ada, conversationId, $"come along /trainings/sessions/{sessionId}");

        // Zoe is not on the team, but the session is public.
        var card = Card(await GetMessagesAsync(zoe, conversationId));
        Assert.Equal("Training", card.GetProperty("kind").GetString());
        Assert.Equal("Open mat", card.GetProperty("title").GetString());
    }

    /// <summary>
    /// <b>SC-007 / FR-040 — the reason for per-viewer resolution.</b> One message, two readers, two
    /// answers: the team member gets the card; the outsider gets nothing but the link they could have
    /// read anyway. A sender cannot use a paste to leak a team-only training's details.
    /// </summary>
    [Fact]
    public async Task The_same_message_shows_a_card_to_a_member_and_a_plain_link_to_an_outsider()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (zoe, zoeId, _) = await NewUserAsync();

        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var sessionId = await SeedTrainingSessionAsync(teamId, adaId, TrainingVisibility.TeamOnly, "Secret tactics session");

        // The very same body, sent into two conversations.
        var withBen = await StartDirectAsync(ada, benId);
        var withZoe = await StartDirectAsync(ada, zoeId);
        var body = $"tonight /trainings/sessions/{sessionId}";
        await SendAsync(ada, withBen, body);
        await SendAsync(ada, withZoe, body);

        // Ben is on the team → card, with the name.
        var benCard = Card(await GetMessagesAsync(ben, withBen));
        Assert.Equal(JsonValueKind.Object, benCard.ValueKind);
        Assert.Equal("Secret tactics session", benCard.GetProperty("title").GetString());

        // Zoe is not → no card at all. The link is still in the body (she could always follow it and be
        // refused there), but none of the training's details leak into the thread.
        var zoePage = await GetMessagesAsync(zoe, withZoe);
        Assert.Equal(JsonValueKind.Null, Card(zoePage).ValueKind);
        Assert.Contains(sessionId.ToString(), zoePage.GetProperty("items")[0].GetProperty("body").GetString());
        Assert.DoesNotContain("Secret tactics session", zoePage.GetProperty("items")[0].GetProperty("body").GetString());
    }

    /// <summary>Even the sender's own view obeys the viewer rule — Ada is on the team, so she sees it.</summary>
    [Fact]
    public async Task The_sender_sees_the_card_when_they_can_see_the_target()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, zoeId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        var sessionId = await SeedTrainingSessionAsync(teamId, adaId, TrainingVisibility.TeamOnly, "Team only");
        var conversationId = await StartDirectAsync(ada, zoeId);

        await SendAsync(ada, conversationId, $"/trainings/sessions/{sessionId}");

        var card = Card(await GetMessagesAsync(ada, conversationId));
        Assert.Equal("Team only", card.GetProperty("title").GetString());
    }

    /// <summary>FR-039: an external URL is plain text, and nothing was fetched to decide that.</summary>
    [Fact]
    public async Task An_external_url_stays_plain_text()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, "look https://example.com/some/page");

        var page = await GetMessagesAsync(ben, conversationId);
        Assert.Equal(JsonValueKind.Null, Card(page).ValueKind);
        Assert.Contains("example.com", page.GetProperty("items")[0].GetProperty("body").GetString());
    }

    /// <summary>FR-041: a deleted target degrades to a plain link rather than erroring.</summary>
    [Fact]
    public async Task A_deleted_target_degrades_to_a_plain_link()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);
        var sessionId = await SeedTrainingSessionAsync(teamId, adaId, TrainingVisibility.Public, "Doomed session");
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, $"/trainings/sessions/{sessionId}");
        Assert.Equal(JsonValueKind.Object, Card(await GetMessagesAsync(ben, conversationId)).ValueKind);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.TrainingSessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync();
        }

        // No error, no broken card — just the link. The link columns are deliberately not FKs.
        var page = await GetMessagesAsync(ben, conversationId);
        Assert.Equal(JsonValueKind.Null, Card(page).ValueKind);
        Assert.Contains(sessionId.ToString(), page.GetProperty("items")[0].GetProperty("body").GetString());
    }

    /// <summary>FR-050c: deleting a message takes its card with it.</summary>
    [Fact]
    public async Task A_deleted_message_surrenders_its_card()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(ada);
        var conversationId = await StartDirectAsync(ada, benId);

        var messageId = await SendAsync(ada, conversationId, $"/t/{slug}");
        Assert.Equal(JsonValueKind.Object, Card(await GetMessagesAsync(ben, conversationId)).ValueKind);

        await ada.DeleteAsync($"/api/v1/chat/messages/{messageId}");

        var page = await GetMessagesAsync(ben, conversationId);
        Assert.Equal(JsonValueKind.Null, Card(page).ValueKind);
        Assert.True(page.GetProperty("items")[0].GetProperty("isDeleted").GetBoolean());
    }

    /// <summary>A link to something that never existed is plain text, not a broken card.</summary>
    [Fact]
    public async Task A_link_to_a_nonexistent_item_stays_plain_text()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, $"/trainings/sessions/{Guid.CreateVersion7()}");

        Assert.Equal(JsonValueKind.Null, Card(await GetMessagesAsync(ben, conversationId)).ValueKind);
    }

    /// <summary>A foreign host wearing our path shape must not render our data (the spoofing case).</summary>
    [Fact]
    public async Task A_link_on_a_foreign_host_never_unfurls()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(ada);
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, $"https://evil.example.com/t/{slug}");

        Assert.Equal(JsonValueKind.Null, Card(await GetMessagesAsync(ben, conversationId)).ValueKind);
    }
}
