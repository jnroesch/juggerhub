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
}
