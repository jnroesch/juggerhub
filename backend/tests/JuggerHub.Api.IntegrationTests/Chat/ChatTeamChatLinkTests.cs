using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// The team page's way into the team chat (feature 060, GH #362): <c>GET /chat/team/{teamId}</c>,
/// and the defect it depends on being fixed — a "contact the admins" thread (feature 027) shares the
/// team's id and used to pass for the team chat.
/// </summary>
/// <remarks>
/// The headline is <see cref="An_admin_gets_the_team_chat_never_a_contact_admins_thread"/>: before the
/// fix, the lookup returned whichever conversation carried the team's id, and an admin is a member of
/// every inquiry thread addressed to their team — so the button would have opened someone's private
/// thread instead of the team's chat.
/// </remarks>
[Collection("Chat")]
public sealed class ChatTeamChatLinkTests : ChatTestSupport
{
    public ChatTeamChatLinkTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- helpers ---------------------------------------------------------------

    private static Task<HttpResponseMessage> OpenTeamChatAsync(HttpClient client, Guid teamId) =>
        client.GetAsync($"/api/v1/chat/team/{teamId}");

    private static async Task<Guid> OpenTeamChatIdAsync(HttpClient client, Guid teamId)
    {
        var resp = await OpenTeamChatAsync(client, teamId);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("conversationId").GetGuid();
    }

    private static async Task<Guid> ContactTeamAsync(HttpClient client, Guid teamId, string body)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/chat/contact/team/{teamId}/messages", new { body });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var sent = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return sent.GetProperty("conversation").GetProperty("id").GetGuid();
    }

    /// <summary>The inbox row for a team's chat — by kind, because an inquiry row carries the team's id too.</summary>
    private static async Task<Guid?> InboxTeamChatIdAsync(HttpClient client, Guid teamId)
    {
        var inbox = await GetInboxAsync(client);
        foreach (var c in inbox.GetProperty("items").EnumerateArray())
        {
            if (c.GetProperty("kind").GetString() == "Team"
                && c.GetProperty("teamId").ValueKind != JsonValueKind.Null
                && c.GetProperty("teamId").GetGuid() == teamId)
            {
                return c.GetProperty("id").GetGuid();
            }
        }

        return null;
    }

    private async Task<int> TeamChatCountAsync(Guid teamId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Conversations.CountAsync(c => c.Kind == ConversationKind.Team && c.TeamId == teamId);
    }

    private async Task<int> NotificationCountAsync(params Guid[] recipients)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Notifications.CountAsync(n => recipients.Contains(n.RecipientUserId));
    }

    // --- US2: a contact-admins thread no longer passes for the team chat ----------

    /// <summary>
    /// FR-013/FR-014. Someone contacts the admins before any member has opened Chat. Before the fix the
    /// inbox saw "a conversation for this team already exists" and never created the team's chat.
    /// </summary>
    [Fact]
    public async Task A_contact_admins_thread_does_not_stop_the_team_chat_being_created()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (jon, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var inquiryId = await ContactTeamAsync(jon, teamId, "Wann trainiert ihr?");

        var teamChatId = await InboxTeamChatIdAsync(ben, teamId);
        Assert.NotNull(teamChatId);
        Assert.NotEqual(inquiryId, teamChatId);

        // The thread with the admins is untouched: same conversation, same message, still Jon's.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inquiry = await db.Conversations.AsNoTracking().SingleAsync(c => c.Id == inquiryId);
        Assert.Equal(ConversationKind.TeamInquiry, inquiry.Kind);
        Assert.Equal(teamId, inquiry.TeamId);
        Assert.Equal(1, await db.ChatMessages.CountAsync(m => m.ConversationId == inquiryId));
    }

    /// <summary>FR-015: still exactly one team chat when first sight races, with an inquiry present.</summary>
    [Fact]
    public async Task A_team_still_has_exactly_one_team_chat_when_an_inquiry_exists()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (jon, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);
        await ContactTeamAsync(jon, teamId, "hallo?");

        await Task.WhenAll(GetInboxAsync(ada), GetInboxAsync(ben), GetInboxAsync(ada));

        Assert.Equal(1, await TeamChatCountAsync(teamId));
    }

    // --- US1: GET /chat/team/{teamId} -------------------------------------------

    /// <summary>FR-002: the button opens the conversation the inbox lists as the team's chat.</summary>
    [Fact]
    public async Task A_member_gets_the_team_chat_the_inbox_lists()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var fromInbox = await InboxTeamChatIdAsync(ben, teamId);
        var fromButton = await OpenTeamChatIdAsync(ben, teamId);

        Assert.Equal(fromInbox, fromButton);
    }

    /// <summary>FR-003: nobody has opened Chat yet — pressing creates the chat and tells nobody anything.</summary>
    [Fact]
    public async Task A_never_opened_team_chat_is_created_and_nothing_is_sent()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);
        Assert.Equal(0, await TeamChatCountAsync(teamId));

        var notificationsBefore = await NotificationCountAsync(adaId, benId);
        var emailsBefore = Factory.EmailSender.Sent.Count;

        var id = await OpenTeamChatIdAsync(ben, teamId);

        Assert.Equal(1, await TeamChatCountAsync(teamId));
        Assert.Equal(0, (await GetMessagesAsync(ben, id)).GetProperty("items").GetArrayLength());
        Assert.Equal(notificationsBefore, await NotificationCountAsync(adaId, benId));
        Assert.Equal(emailsBefore, Factory.EmailSender.Sent.Count);
        Assert.DoesNotContain(adaId, Factory.PushDispatcher.Recipients);
        Assert.DoesNotContain(benId, Factory.PushDispatcher.Recipients);
    }

    /// <summary>
    /// <b>FR-002 / SC-002.</b> An admin is a member of every contact-admins thread addressed to their
    /// team. The lookup must return the team chat regardless — including when the inquiry came first.
    /// </summary>
    [Fact]
    public async Task An_admin_gets_the_team_chat_never_a_contact_admins_thread()
    {
        var (ada, _, _) = await NewUserAsync();
        var (jon, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        var inquiryId = await ContactTeamAsync(jon, teamId, "Nehmt ihr noch Leute?");

        var id = await OpenTeamChatIdAsync(ada, teamId);

        Assert.NotEqual(inquiryId, id);
        var detail = await ada.GetFromJsonAsync<JsonElement>($"/api/v1/chat/conversations/{id}", Json);
        Assert.Equal("Team", detail.GetProperty("kind").GetString());
        Assert.Equal(teamId, detail.GetProperty("teamId").GetGuid());
    }

    /// <summary>FR-005/FR-007: a non-member is refused, and asking creates nothing for that team.</summary>
    [Fact]
    public async Task A_non_member_gets_404_and_creates_no_chat_for_the_team()
    {
        var (ada, _, _) = await NewUserAsync();
        var (jon, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var resp = await OpenTeamChatAsync(jon, teamId);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await TeamChatCountAsync(teamId));
    }

    /// <summary>FR-001: a pending join request is not membership.</summary>
    [Fact]
    public async Task A_pending_join_request_does_not_open_the_team_chat()
    {
        var (ada, _, _) = await NewUserAsync();
        var (rhea, rheaId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TeamJoinRequests.Add(new TeamJoinRequest { TeamId = teamId, UserId = rheaId });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await OpenTeamChatAsync(rhea, teamId)).StatusCode);
    }

    /// <summary>
    /// <b>FR-005 / SC-003.</b> Not a member, and no such team, read the same — so the endpoint cannot be
    /// used to learn whether a team exists or has a chat.
    /// </summary>
    [Fact]
    public async Task A_refusal_reads_the_same_whether_or_not_the_team_exists()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (jon, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);
        await OpenTeamChatIdAsync(ben, teamId); // the team's chat exists

        var notMember = await OpenTeamChatAsync(jon, teamId);
        var noSuchTeam = await OpenTeamChatAsync(jon, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, notMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noSuchTeam.StatusCode);
        var a = await notMember.Content.ReadFromJsonAsync<JsonElement>(Json);
        var b = await noSuchTeam.Content.ReadFromJsonAsync<JsonElement>(Json);
        foreach (var field in new[] { "title", "detail", "status" })
        {
            Assert.Equal(a.GetProperty(field).ToString(), b.GetProperty(field).ToString());
        }
    }

    /// <summary>FR-012: hidden and muted, it still opens — and pressing changes neither flag.</summary>
    [Fact]
    public async Task A_hidden_and_muted_team_chat_still_opens_and_stays_so()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);
        var id = await OpenTeamChatIdAsync(ben, teamId);

        var patch = await ben.PatchAsJsonAsync($"/api/v1/chat/conversations/{id}/state", new { isMuted = true, isHidden = true });
        Assert.Equal(HttpStatusCode.NoContent, patch.StatusCode);

        Assert.Equal(id, await OpenTeamChatIdAsync(ben, teamId));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.ConversationParticipants.AsNoTracking().SingleAsync(p => p.ConversationId == id && p.UserId == benId);
        Assert.True(state.IsHidden);
        Assert.True(state.IsMuted);
    }

    /// <summary>FR-006: the answer is the id and nothing else.</summary>
    [Fact]
    public async Task The_answer_carries_only_the_conversation_id()
    {
        var (ada, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var resp = await OpenTeamChatAsync(ada, teamId);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);

        var property = Assert.Single(body.EnumerateObject());
        Assert.Equal("conversationId", property.Name);
    }

    /// <summary>FR-015: two members pressing at once on a never-opened chat land in the same, single chat.</summary>
    [Fact]
    public async Task Concurrent_first_presses_leave_one_team_chat()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var ids = await Task.WhenAll(
            OpenTeamChatIdAsync(ada, teamId),
            OpenTeamChatIdAsync(ben, teamId),
            OpenTeamChatIdAsync(ada, teamId));

        Assert.Single(ids.Distinct());
        Assert.Equal(1, await TeamChatCountAsync(teamId));
    }

    [Fact]
    public async Task Without_a_session_the_answer_is_401()
    {
        var (ada, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var resp = await OpenTeamChatAsync(Factory.CreateClient(), teamId);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
