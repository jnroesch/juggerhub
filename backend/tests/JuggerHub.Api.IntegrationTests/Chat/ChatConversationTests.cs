using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Starting conversations and reading the inbox (feature 019, User Story 1).
/// </summary>
[Collection("Chat")]
public sealed class ChatConversationTests : ChatTestSupport
{
    public ChatConversationTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Starting_a_direct_conversation_creates_it_and_both_players_see_it()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);

        var adaInbox = await GetInboxAsync(ada);
        Assert.Contains(adaInbox.GetProperty("items").EnumerateArray(),
            c => c.GetProperty("id").GetGuid() == conversationId);

        // Ben sees it too, once there is a message in it.
        await SendAsync(ada, conversationId, "hey");
        var benInbox = await GetInboxAsync(ben);
        Assert.Contains(benInbox.GetProperty("items").EnumerateArray(),
            c => c.GetProperty("id").GetGuid() == conversationId);
    }

    /// <summary>FR-008: at most one direct conversation per pair — starting again opens the same one.</summary>
    [Fact]
    public async Task Starting_a_direct_conversation_twice_returns_the_same_conversation()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var first = await StartDirectAsync(ada, benId);
        var second = await StartDirectAsync(ada, benId);

        Assert.Equal(first, second);
    }

    /// <summary>FR-008 from the other side: the pair key is order-independent.</summary>
    [Fact]
    public async Task Either_player_starting_the_dm_lands_in_the_same_conversation()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var fromAda = await StartDirectAsync(ada, benId);
        var fromBen = await StartDirectAsync(ben, adaId);

        Assert.Equal(fromAda, fromBen);
    }

    /// <summary>
    /// FR-049: reach is open. Ada and Zoe share no team, party or event, and that is fine — the picker
    /// merely lists teammates first. This test is the guard against someone "helpfully" adding a
    /// shared-context restriction later.
    /// </summary>
    [Fact]
    public async Task A_player_can_dm_someone_they_share_no_context_with()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, zoeId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, zoeId);

        Assert.NotEqual(Guid.Empty, conversationId);
    }

    [Fact]
    public async Task Starting_a_chat_with_only_yourself_is_rejected()
    {
        var (ada, adaId, _) = await NewUserAsync();

        var resp = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { adaId }, name = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Starting_a_chat_with_an_unknown_player_is_rejected()
    {
        var (ada, _, _) = await NewUserAsync();

        var resp = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { Guid.CreateVersion7() }, name = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    /// <summary>
    /// FR-048 / SC-004: a non-member gets <b>404</b>, never 403. A 403 would confirm the conversation
    /// exists, which is itself a leak — "there is a conversation here you're not in" is information.
    /// </summary>
    [Fact]
    public async Task A_non_member_gets_404_not_403_for_someone_elses_conversation()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (mallory, _, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);

        var detail = await mallory.GetAsync($"/api/v1/chat/conversations/{conversationId}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var messages = await mallory.GetAsync($"/api/v1/chat/conversations/{conversationId}/messages");
        Assert.Equal(HttpStatusCode.NotFound, messages.StatusCode);

        var members = await mallory.GetAsync($"/api/v1/chat/conversations/{conversationId}/members");
        Assert.Equal(HttpStatusCode.NotFound, members.StatusCode);
    }

    /// <summary>FR-048: an entirely made-up id is indistinguishable from someone else's real one.</summary>
    [Fact]
    public async Task An_unknown_conversation_id_looks_exactly_like_someone_elses()
    {
        var (ada, _, _) = await NewUserAsync();

        var resp = await ada.GetAsync($"/api/v1/chat/conversations/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    /// <summary>FR-006 / SC-010: the inbox is paginated, never unbounded.</summary>
    [Fact]
    public async Task The_inbox_is_paginated()
    {
        var (ada, _, _) = await NewUserAsync();

        for (var i = 0; i < 3; i++)
        {
            var (_, otherId, _) = await NewUserAsync();
            var id = await StartDirectAsync(ada, otherId);
            await SendAsync(ada, id, $"message {i}");
        }

        var resp = await ada.GetAsync("/api/v1/chat/conversations?skip=0&take=2");
        resp.EnsureSuccessStatusCode();
        var page = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(2, page.GetProperty("items").GetArrayLength());
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 3);
        Assert.Equal(2, page.GetProperty("take").GetInt32());
    }

    /// <summary>The inbox surfaces the most recently active conversation first.</summary>
    [Fact]
    public async Task The_inbox_orders_most_recently_active_first()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();

        var withBen = await StartDirectAsync(ada, benId);
        var withNia = await StartDirectAsync(ada, niaId);

        await SendAsync(ada, withBen, "first");
        await SendAsync(ada, withNia, "second");
        await SendAsync(ada, withBen, "third — most recent");

        var inbox = await GetInboxAsync(ada);
        var ids = inbox.GetProperty("items").EnumerateArray()
            .Select(c => c.GetProperty("id").GetGuid())
            .ToList();

        Assert.Equal(withBen, ids[0]);
        Assert.Equal(withNia, ids[1]);
    }

    /// <summary>A group needs a name (FR-009); a DM must not have one.</summary>
    [Fact]
    public async Task A_group_requires_a_name()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();

        var unnamed = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { benId, niaId }, name = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, unnamed.StatusCode);

        var blank = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { benId, niaId }, name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var named = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { benId, niaId }, name = "Weekend crew" });
        Assert.True(named.IsSuccessStatusCode);

        var body = await named.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Group", body.GetProperty("kind").GetString());
        Assert.Equal("Weekend crew", body.GetProperty("name").GetString());
    }

    /// <summary>Selecting exactly one person yields a Direct conversation, not a one-person group.</summary>
    [Fact]
    public async Task One_participant_makes_a_direct_conversation_not_a_group()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var resp = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { benId }, name = "ignored" });
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Direct", body.GetProperty("kind").GetString());
    }
}
