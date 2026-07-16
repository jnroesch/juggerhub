using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Archiving an auto chat when its team/party goes away (feature 019, FR-027 / data-model R3a).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the file that would have caught the original design flaw.</b> The plan assumed archiving
/// could be a state flag. It cannot: team delete and party disband are <em>hard deletes</em> whose
/// rosters cascade, and a live auto chat <em>derives</em> its membership from that roster — so a
/// flag-only archive would leave a conversation literally nobody can read, silently breaking FR-027's
/// "members can still read the history". Archiving therefore snapshots the roster into real
/// participant rows first.
/// </para>
/// <para>
/// <see cref="A_former_member_can_still_read_an_archived_team_chat"/> is the assertion that pins it.
/// </para>
/// </remarks>
[Collection("Chat")]
public sealed class ChatArchiveTests : ChatTestSupport
{
    public ChatArchiveTests(JuggerHubApiFactory factory) : base(factory) { }

    private async Task<Guid> TeamChatIdAsync(HttpClient client, Guid teamId)
    {
        var inbox = await GetInboxAsync(client);
        return inbox.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("teamId").ValueKind != JsonValueKind.Null && c.GetProperty("teamId").GetGuid() == teamId)
            .GetProperty("id").GetGuid();
    }

    /// <summary>
    /// The whole point of R3a: the roster is gone, but the people who were in it can still read.
    /// </summary>
    [Fact]
    public async Task A_former_member_can_still_read_an_archived_team_chat()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var conversationId = await TeamChatIdAsync(ben, teamId);
        await SendAsync(ada, conversationId, "last training was great");

        // Delete the team — a hard delete that cascades TeamMemberships away.
        var delete = await ada.DeleteAsync($"/api/v1/teams/{slug}");
        Assert.True(delete.IsSuccessStatusCode, $"team delete failed: {(int)delete.StatusCode}");

        // The team really is gone: the Restrict FK did not block it, because archiving cleared the link.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Teams.AnyAsync(t => t.Id == teamId));

            var conversation = await db.Conversations.AsNoTracking().FirstAsync(c => c.Id == conversationId);
            Assert.Equal(ConversationState.Archived, conversation.State);
            Assert.Null(conversation.TeamId);
            // The name was frozen — there is no team left to derive it from.
            Assert.False(string.IsNullOrWhiteSpace(conversation.Name));
            // And Kind is deliberately unchanged, so the inbox still tags it TEAM.
            Assert.Equal(ConversationKind.Team, conversation.Kind);
        }

        // The history is still readable by someone who was on the roster — this is the assertion a
        // flag-only archive fails, because the roster it derived membership from no longer exists.
        var page = await GetMessagesAsync(ben, conversationId);
        Assert.Contains(page.GetProperty("items").EnumerateArray(),
            m => m.GetProperty("body").GetString() == "last training was great");

        var inbox = await GetInboxAsync(ben);
        var row = inbox.GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conversationId);
        Assert.Equal("Archived", row.GetProperty("state").GetString());
    }

    [Fact]
    public async Task An_archived_chat_is_read_only()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var conversationId = await TeamChatIdAsync(ben, teamId);
        await ada.DeleteAsync($"/api/v1/teams/{slug}");

        // No new messages…
        var send = await ben.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages", new { body = "hello?" });
        Assert.Equal(HttpStatusCode.Conflict, send.StatusCode);

        // …and no typing.
        var typing = await ben.PostAsync($"/api/v1/chat/conversations/{conversationId}/typing", null);
        Assert.Equal(HttpStatusCode.Conflict, typing.StatusCode);
    }

    [Fact]
    public async Task A_non_member_still_cannot_read_an_archived_chat()
    {
        var (ada, _, _) = await NewUserAsync();
        var (zoe, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(ada);

        var conversationId = await TeamChatIdAsync(ada, teamId);
        await ada.DeleteAsync($"/api/v1/teams/{slug}");

        // Archiving snapshots the roster — it must not accidentally widen access to everyone.
        Assert.Equal(HttpStatusCode.NotFound, (await zoe.GetAsync($"/api/v1/chat/conversations/{conversationId}")).StatusCode);
    }

    /// <summary>Archiving is one-way and idempotent (data-model R3).</summary>
    [Fact]
    public async Task Archiving_twice_is_idempotent_and_never_reopens()
    {
        var (ada, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(ada);
        var conversationId = await TeamChatIdAsync(ada, teamId);

        await ada.DeleteAsync($"/api/v1/teams/{slug}");

        using var scope = Factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatConversationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // A second archive (e.g. a retried delete) must not throw or resurrect anything.
        await chat.ArchiveForTeamAsync(teamId);

        var conversation = await db.Conversations.AsNoTracking().FirstAsync(c => c.Id == conversationId);
        Assert.Equal(ConversationState.Archived, conversation.State);
    }

    /// <summary>A team nobody ever chatted in has no conversation — archiving must not invent one.</summary>
    [Fact]
    public async Task Deleting_a_team_whose_chat_was_never_opened_is_fine()
    {
        var (ada, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(ada);

        // Note: no inbox call, so the ensure path never ran.
        var delete = await ada.DeleteAsync($"/api/v1/teams/{slug}");
        Assert.True(delete.IsSuccessStatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Conversations.AnyAsync(c => c.TeamId == teamId));
    }
}
