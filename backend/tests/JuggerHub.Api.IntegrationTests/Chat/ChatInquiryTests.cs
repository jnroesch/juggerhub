using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// "Contact the admins" inquiry threads for teams and events (feature 027). A mirrored conversation
/// kind whose admin side derives from the team/event admin roster, with the requester the one fixed
/// participant — created on first send, reused per pair, distinguishable in the inbox, and archived by
/// snapshot when the team is deleted or the event cancelled.
/// </summary>
[Collection("Chat")]
public sealed class ChatInquiryTests : ChatTestSupport
{
    public ChatInquiryTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- helpers ---------------------------------------------------------------

    private async Task<Guid> SeedEventAsync(Guid adminUserId, EventStatus status = EventStatus.Published)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ev = new Event
        {
            Name = "Sommerturnier",
            Type = EventType.Tournament,
            Description = "A tournament",
            StartsAt = DateTime.UtcNow.AddDays(14),
            EndsAt = DateTime.UtcNow.AddDays(15),
            LocationKind = LocationKind.InPerson,
            Location = "Köln",
            ParticipantMode = ParticipantMode.Individuals,
            ParticipationLimit = 32,
            Status = status,
            CancelledDate = status == EventStatus.Cancelled ? DateTime.UtcNow : null,
        };
        db.Events.Add(ev);
        db.EventAdmins.Add(new EventAdmin { EventId = ev.Id, UserId = adminUserId, AddedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return ev.Id;
    }

    private async Task AddEventAdminAsync(Guid eventId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EventAdmins.Add(new EventAdmin { EventId = eventId, UserId = userId, AddedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private static async Task<(HttpResponseMessage Resp, Guid ConversationId, Guid MessageId)> ContactTeamAsync(
        HttpClient client, Guid teamId, string body)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/chat/contact/team/{teamId}/messages", new { body });
        if (!resp.IsSuccessStatusCode)
        {
            return (resp, Guid.Empty, Guid.Empty);
        }

        var sent = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (resp, sent.GetProperty("conversation").GetProperty("id").GetGuid(), sent.GetProperty("message").GetProperty("id").GetGuid());
    }

    private static async Task<(HttpResponseMessage Resp, Guid ConversationId, Guid MessageId)> ContactEventAsync(
        HttpClient client, Guid eventId, string body)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/chat/contact/event/{eventId}/messages", new { body });
        if (!resp.IsSuccessStatusCode)
        {
            return (resp, Guid.Empty, Guid.Empty);
        }

        var sent = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (resp, sent.GetProperty("conversation").GetProperty("id").GetGuid(), sent.GetProperty("message").GetProperty("id").GetGuid());
    }

    private async Task<string> DisplayNameOfAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PlayerProfiles.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.DisplayName).FirstAsync();
    }

    private static JsonElement Row(JsonElement inbox, Guid conversationId) =>
        inbox.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == conversationId);

    // --- US1: messaging works end to end --------------------------------------

    [Fact]
    public async Task Team_inquiry_first_send_creates_thread_and_reaches_both_admins()
    {
        var (ada, adaId, _) = await NewUserAsync();      // team admin (creator)
        var (nia, niaId, _) = await NewUserAsync();      // second admin
        var (pat, _, _) = await NewUserAsync();          // asking player
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, niaId, TeamRole.Admin);

        var (resp, conversationId, _) = await ContactTeamAsync(pat, teamId, "When is the next training?");
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        // Both current admins see the thread and the message; the asking player does too.
        foreach (var admin in new[] { ada, nia })
        {
            var page = await GetMessagesAsync(admin, conversationId);
            Assert.Contains(page.GetProperty("items").EnumerateArray(),
                m => m.GetProperty("body").GetString() == "When is the next training?");
        }
    }

    [Fact]
    public async Task Second_contact_reuses_the_same_thread()
    {
        var (ada, _, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var (_, first, _) = await ContactTeamAsync(pat, teamId, "hello");
        var (_, second, _) = await ContactTeamAsync(pat, teamId, "still there?");

        Assert.Equal(first, second);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Conversations.CountAsync(c => c.Kind == ConversationKind.TeamInquiry
            && c.TeamId == teamId && c.RequesterUserId != null);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Admin_reply_is_readable_by_the_requester()
    {
        var (ada, _, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var (_, conversationId, _) = await ContactTeamAsync(pat, teamId, "question");
        await SendAsync(ada, conversationId, "great question — Saturday 10am");

        var page = await GetMessagesAsync(pat, conversationId);
        Assert.Contains(page.GetProperty("items").EnumerateArray(),
            m => m.GetProperty("body").GetString() == "great question — Saturday 10am");
    }

    [Fact]
    public async Task Event_inquiry_first_send_reaches_the_event_admin()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var eventId = await SeedEventAsync(adaId);

        var (resp, conversationId, _) = await ContactEventAsync(pat, eventId, "Is there parking?");
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var page = await GetMessagesAsync(ada, conversationId);
        Assert.Contains(page.GetProperty("items").EnumerateArray(),
            m => m.GetProperty("body").GetString() == "Is there parking?");
    }

    // --- US1: guardrails -------------------------------------------------------

    [Fact]
    public async Task An_admin_cannot_start_an_inquiry_to_their_own_team()
    {
        var (ada, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var (resp, _, _) = await ContactTeamAsync(ada, teamId, "talking to myself");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Conversations.AnyAsync(c => c.Kind == ConversationKind.TeamInquiry && c.TeamId == teamId));
    }

    [Fact]
    public async Task Inquiry_to_a_missing_team_is_404()
    {
        var (pat, _, _) = await NewUserAsync();
        var (resp, _, _) = await ContactTeamAsync(pat, Guid.NewGuid(), "hello?");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Inquiry_to_a_cancelled_event_is_409()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var eventId = await SeedEventAsync(adaId, EventStatus.Cancelled);

        var (resp, _, _) = await ContactEventAsync(pat, eventId, "still on?");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // --- US2: distinguishable naming ------------------------------------------

    [Fact]
    public async Task Requester_sees_team_name_and_admin_sees_requester_plus_team_context()
    {
        var (ada, _, _) = await NewUserAsync();
        var (pat, patId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);   // team name is "Rheinfeuer"

        var (_, conversationId, _) = await ContactTeamAsync(pat, teamId, "hi");

        // The asking player sees WHAT it's about — the team name.
        var patRow = Row(await GetInboxAsync(pat), conversationId);
        Assert.Equal("Rheinfeuer", patRow.GetProperty("name").GetString());
        Assert.Equal("TeamInquiry", patRow.GetProperty("kind").GetString());

        // The admin sees WHO is asking AND which team it concerns, so several inquiries are tellable apart.
        var adaRow = Row(await GetInboxAsync(ada), conversationId);
        var patName = await DisplayNameOfAsync(patId);
        Assert.Equal($"{patName} · Rheinfeuer", adaRow.GetProperty("name").GetString());
        Assert.Equal("TeamInquiry", adaRow.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task An_inquiry_thread_cannot_be_left_but_can_be_muted()
    {
        var (ada, _, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var (_, conversationId, _) = await ContactTeamAsync(pat, teamId, "hi");

        // A mirrored kind: the detail reports it as non-leavable / non-addable (FR-017).
        var detail = await pat.GetFromJsonAsync<JsonElement>($"/api/v1/chat/conversations/{conversationId}", Json);
        Assert.False(detail.GetProperty("canLeave").GetBoolean());
        Assert.False(detail.GetProperty("canAddMembers").GetBoolean());

        // Leaving is refused…
        var leave = await pat.DeleteAsync($"/api/v1/chat/conversations/{conversationId}/members/me");
        Assert.Equal(HttpStatusCode.BadRequest, leave.StatusCode);

        // …but muting (the stand-in) succeeds.
        var mute = await pat.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/state", new { isMuted = true });
        Assert.True(mute.IsSuccessStatusCode);
    }

    // --- US3: membership follows the roster -----------------------------------

    [Fact]
    public async Task A_newly_granted_admin_gains_access_but_not_history_before_the_grant()
    {
        var (ada, _, _) = await NewUserAsync();
        var (nia, niaId, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var (_, conversationId, _) = await ContactTeamAsync(pat, teamId, "posted before Nia was admin");

        // Nia is not an admin yet — cannot see it.
        Assert.Equal(HttpStatusCode.NotFound, (await nia.GetAsync($"/api/v1/chat/conversations/{conversationId}")).StatusCode);

        await AddTeamMemberAsync(teamId, niaId, TeamRole.Admin);
        await SendAsync(pat, conversationId, "posted after Nia was admin");

        // Now Nia can read — but only from her grant forward (FR-019).
        var page = await GetMessagesAsync(nia, conversationId);
        var bodies = page.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("body").GetString()).ToList();
        Assert.Contains("posted after Nia was admin", bodies);
        Assert.DoesNotContain("posted before Nia was admin", bodies);
    }

    [Fact]
    public async Task A_removed_admin_loses_access_with_404_not_403()
    {
        var (ada, _, _) = await NewUserAsync();
        var (nia, niaId, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, niaId, TeamRole.Admin);

        var (_, conversationId, _) = await ContactTeamAsync(pat, teamId, "hi admins");
        Assert.True((await nia.GetAsync($"/api/v1/chat/conversations/{conversationId}")).IsSuccessStatusCode);

        await RemoveTeamMemberAsync(teamId, niaId);

        Assert.Equal(HttpStatusCode.NotFound, (await nia.GetAsync($"/api/v1/chat/conversations/{conversationId}")).StatusCode);
    }

    [Fact]
    public async Task Demoting_an_admin_to_member_loses_inquiry_access()
    {
        var (ada, _, _) = await NewUserAsync();
        var (nia, niaId, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, niaId, TeamRole.Admin);

        var (_, conversationId, _) = await ContactTeamAsync(pat, teamId, "hi");
        Assert.True((await nia.GetAsync($"/api/v1/chat/conversations/{conversationId}")).IsSuccessStatusCode);

        // Demote to plain member — an inquiry is admins-only, so access must go even though Nia stays on the team.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.TeamMemberships.Where(m => m.TeamId == teamId && m.UserId == niaId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, TeamRole.Member));
        }

        Assert.Equal(HttpStatusCode.NotFound, (await nia.GetAsync($"/api/v1/chat/conversations/{conversationId}")).StatusCode);
    }

    // --- US3: archival ---------------------------------------------------------

    [Fact]
    public async Task Deleting_a_team_archives_its_inquiry_threads_readable_and_closed()
    {
        var (ada, _, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(ada);

        var (_, conversationId, _) = await ContactTeamAsync(pat, teamId, "pre-delete question");

        var delete = await ada.DeleteAsync($"/api/v1/teams/{slug}");
        Assert.True(delete.IsSuccessStatusCode, $"team delete failed: {(int)delete.StatusCode}");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Teams.AnyAsync(t => t.Id == teamId));
            var conversation = await db.Conversations.AsNoTracking().FirstAsync(c => c.Id == conversationId);
            Assert.Equal(ConversationState.Archived, conversation.State);
            Assert.Null(conversation.TeamId);
            Assert.False(string.IsNullOrWhiteSpace(conversation.Name));
            Assert.Equal(ConversationKind.TeamInquiry, conversation.Kind);
        }

        // The requester can still read the history…
        var page = await GetMessagesAsync(pat, conversationId);
        Assert.Contains(page.GetProperty("items").EnumerateArray(),
            m => m.GetProperty("body").GetString() == "pre-delete question");

        // …but the thread is closed to writes.
        var send = await pat.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages", new { body = "anyone?" });
        Assert.Equal(HttpStatusCode.Conflict, send.StatusCode);
    }

    [Fact]
    public async Task Cancelling_an_event_archives_its_inquiry_threads()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (pat, _, _) = await NewUserAsync();
        var eventId = await SeedEventAsync(adaId);

        var (_, conversationId, _) = await ContactEventAsync(pat, eventId, "pre-cancel question");

        using (var scope = Factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<IEventService>();
            var status = await events.CancelAsync(eventId, adaId);
            Assert.Equal(CancelEventStatus.Cancelled, status);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conversation = await db.Conversations.AsNoTracking().FirstAsync(c => c.Id == conversationId);
            Assert.Equal(ConversationState.Archived, conversation.State);
            Assert.Null(conversation.EventId);
            Assert.Equal(ConversationKind.EventInquiry, conversation.Kind);
        }

        // History still readable by the requester.
        var page = await GetMessagesAsync(pat, conversationId);
        Assert.Contains(page.GetProperty("items").EnumerateArray(),
            m => m.GetProperty("body").GetString() == "pre-cancel question");
    }
}
