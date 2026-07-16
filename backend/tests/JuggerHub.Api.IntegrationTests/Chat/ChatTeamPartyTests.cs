using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Auto-created team and party chats (feature 019, User Story 4).
/// </summary>
/// <remarks>
/// The headline is <see cref="Leaving_the_team_revokes_chat_access"/> — SC-004. Membership is
/// <em>derived</em> from the roster rather than mirrored into rows, so revocation is true by
/// construction; this test is what proves the derivation is actually wired up rather than assumed.
/// </remarks>
[Collection("Chat")]
public sealed class ChatTeamPartyTests : ChatTestSupport
{
    public ChatTeamPartyTests(JuggerHubApiFactory factory) : base(factory) { }

    private static async Task<JsonElement?> FindTeamChatAsync(HttpClient client, Guid teamId)
    {
        var inbox = await GetInboxAsync(client);
        foreach (var c in inbox.GetProperty("items").EnumerateArray())
        {
            if (c.GetProperty("teamId").ValueKind != JsonValueKind.Null
                && c.GetProperty("teamId").GetGuid() == teamId)
            {
                return c;
            }
        }

        return null;
    }

    /// <summary>
    /// FR-024: the chat is simply there. Nobody created it, and the team pre-dates the feature — which
    /// is the backfill requirement, satisfied by ensure-on-access rather than a migration.
    /// </summary>
    [Fact]
    public async Task A_team_chat_appears_by_itself_with_the_roster()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var chat = await FindTeamChatAsync(ben, teamId);
        Assert.NotNull(chat);
        Assert.Equal("Team", chat!.Value.GetProperty("kind").GetString());

        var conversationId = chat.Value.GetProperty("id").GetGuid();
        var members = await ben.GetFromJsonAsync<JsonElement>(
            $"/api/v1/chat/conversations/{conversationId}/members", Json);

        var memberIds = members.GetProperty("items").EnumerateArray()
            .Select(m => m.GetProperty("userId").GetGuid())
            .ToList();

        Assert.Contains(adaId, memberIds);
        Assert.Contains(benId, memberIds);
    }

    /// <summary>Exactly one chat per team, even when two members open Chat at the same moment (FR-024).</summary>
    [Fact]
    public async Task A_team_has_exactly_one_chat_under_concurrent_first_access()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        // Both open the inbox at once — the ensure path races.
        await Task.WhenAll(GetInboxAsync(ada), GetInboxAsync(ben), GetInboxAsync(ada));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Conversations.CountAsync(c => c.TeamId == teamId);

        Assert.Equal(1, count);
    }

    /// <summary>
    /// <b>SC-004 / FR-025.</b> Removed from the roster ⇒ removed from the chat, with no sync step —
    /// and a direct request returns 404, not 403, so the chat's existence does not leak either.
    /// </summary>
    [Fact]
    public async Task Leaving_the_team_revokes_chat_access()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var chat = await FindTeamChatAsync(ben, teamId);
        Assert.NotNull(chat);
        var conversationId = chat!.Value.GetProperty("id").GetGuid();
        await SendAsync(ada, conversationId, "team stuff");

        // Ben leaves the roster.
        await RemoveTeamMemberAsync(teamId, benId);

        // Gone from the inbox…
        Assert.Null(await FindTeamChatAsync(ben, teamId));

        // …and refused on a direct request, driving the API rather than the UI.
        Assert.Equal(HttpStatusCode.NotFound, (await ben.GetAsync($"/api/v1/chat/conversations/{conversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.GetAsync($"/api/v1/chat/conversations/{conversationId}/messages")).StatusCode);

        var send = await ben.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages", new { body = "let me back" });
        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);
    }

    /// <summary>Edge case: rejoining restores access and the history.</summary>
    [Fact]
    public async Task Rejoining_the_team_restores_access_and_history()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var chat = await FindTeamChatAsync(ben, teamId);
        var conversationId = chat!.Value.GetProperty("id").GetGuid();
        await SendAsync(ada, conversationId, "before ben left");

        await RemoveTeamMemberAsync(teamId, benId);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.GetAsync($"/api/v1/chat/conversations/{conversationId}")).StatusCode);

        await AddTeamMemberAsync(teamId, benId);

        var page = await GetMessagesAsync(ben, conversationId);
        Assert.Contains(page.GetProperty("items").EnumerateArray(), m => m.GetProperty("body").GetString() == "before ben left");
    }

    /// <summary>FR-026 / US4 #4-#5: a team chat cannot be left or added to — mute and hide instead.</summary>
    [Fact]
    public async Task A_team_chat_cannot_be_left_or_added_to()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        var chat = await FindTeamChatAsync(ada, teamId);
        var conversationId = chat!.Value.GetProperty("id").GetGuid();

        var leave = await ada.DeleteAsync($"/api/v1/chat/conversations/{conversationId}/members/me");
        Assert.Equal(HttpStatusCode.BadRequest, leave.StatusCode);

        var add = await ada.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/members",
            new { userIds = new[] { Guid.CreateVersion7() } });
        Assert.Equal(HttpStatusCode.BadRequest, add.StatusCode);

        // The details panel says so too, so the UI never offers what the server would refuse.
        var detail = await ada.GetFromJsonAsync<JsonElement>($"/api/v1/chat/conversations/{conversationId}", Json);
        Assert.False(detail.GetProperty("canLeave").GetBoolean());
        Assert.False(detail.GetProperty("canAddMembers").GetBoolean());

        // But mute works — the substitute for leaving.
        var mute = await ada.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/state", new { isMuted = true });
        Assert.Equal(HttpStatusCode.NoContent, mute.StatusCode);
    }

    /// <summary>A single-member team still gets a usable chat (edge case).</summary>
    [Fact]
    public async Task A_one_member_team_chat_is_usable()
    {
        var (ada, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var chat = await FindTeamChatAsync(ada, teamId);
        Assert.NotNull(chat);

        var conversationId = chat!.Value.GetProperty("id").GetGuid();
        await SendAsync(ada, conversationId, "note to self");

        var page = await GetMessagesAsync(ada, conversationId);
        Assert.Single(page.GetProperty("items").EnumerateArray());
    }

    /// <summary>A non-member never sees a team chat, however they ask.</summary>
    [Fact]
    public async Task An_outsider_cannot_reach_a_team_chat()
    {
        var (ada, _, _) = await NewUserAsync();
        var (zoe, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);

        var chat = await FindTeamChatAsync(ada, teamId);
        var conversationId = chat!.Value.GetProperty("id").GetGuid();

        Assert.Null(await FindTeamChatAsync(zoe, teamId));
        Assert.Equal(HttpStatusCode.NotFound, (await zoe.GetAsync($"/api/v1/chat/conversations/{conversationId}")).StatusCode);
    }
}
