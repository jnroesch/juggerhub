using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Blocking, muting and hiding (feature 019, User Story 5).
/// </summary>
/// <remarks>
/// Blocking is load-bearing here rather than a nicety: DM reach is open by product decision
/// (FR-049), so this is the only recourse against unwanted contact. Every block assertion below
/// therefore drives the <b>API directly</b>, not the UI — hiding a button is not enforcement
/// (SC-005).
/// </remarks>
[Collection("Chat")]
public sealed class ChatBlockTests : ChatTestSupport
{
    public ChatBlockTests(JuggerHubApiFactory factory) : base(factory) { }

    private static Task<HttpResponseMessage> BlockViaApiAsync(HttpClient client, Guid targetUserId) =>
        client.PostAsJsonAsync("/api/v1/chat/blocks", new { userId = targetUserId });

    /// <summary>SC-005: the blocked player's send is refused server-side, bypassing the interface entirely.</summary>
    [Fact]
    public async Task A_blocked_player_cannot_send_to_an_existing_dm()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (zoe, zoeId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(zoe, adaId);
        await SendAsync(zoe, conversationId, "hi");

        (await BlockViaApiAsync(ada, zoeId)).EnsureSuccessStatusCode();

        var send = await zoe.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages",
            new { body = "hello again" });

        Assert.Equal(HttpStatusCode.Forbidden, send.StatusCode);

        // And nothing was delivered.
        var page = await GetMessagesAsync(ada, conversationId);
        Assert.DoesNotContain(page.GetProperty("items").EnumerateArray(),
            m => m.GetProperty("body").GetString() == "hello again");
    }

    /// <summary>FR-049b: a block cannot be walked around by starting a fresh conversation.</summary>
    [Fact]
    public async Task A_blocked_player_cannot_start_a_new_dm_with_the_blocker()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (zoe, zoeId, _) = await NewUserAsync();

        (await BlockViaApiAsync(ada, zoeId)).EnsureSuccessStatusCode();

        var start = await zoe.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { adaId }, name = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
    }

    /// <summary>The blocker cannot message them either — the check is symmetric on the conversation.</summary>
    [Fact]
    public async Task The_blocker_also_cannot_start_a_dm_with_someone_they_blocked()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, zoeId, _) = await NewUserAsync();

        (await BlockViaApiAsync(ada, zoeId)).EnsureSuccessStatusCode();

        var start = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { zoeId }, name = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
    }

    /// <summary>R19: the blocker's inbox stops showing the thread.</summary>
    [Fact]
    public async Task The_blocked_dm_leaves_the_blockers_inbox()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (zoe, zoeId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(zoe, adaId);
        await SendAsync(zoe, conversationId, "hi");

        Assert.Contains((await GetInboxAsync(ada)).GetProperty("items").EnumerateArray(),
            c => c.GetProperty("id").GetGuid() == conversationId);

        (await BlockViaApiAsync(ada, zoeId)).EnsureSuccessStatusCode();

        Assert.DoesNotContain((await GetInboxAsync(ada)).GetProperty("items").EnumerateArray(),
            c => c.GetProperty("id").GetGuid() == conversationId);
    }

    /// <summary>
    /// <b>FR-032 — the carve-out.</b> A block is about direct messages. Two people who both belong to a
    /// team or a group keep participating normally; blocking must not fracture a shared space.
    /// </summary>
    [Fact]
    public async Task A_block_does_not_touch_a_shared_group()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (zoe, zoeId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var groupResp = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { zoeId, benId }, name = "Weekend crew" });
        groupResp.EnsureSuccessStatusCode();
        var groupId = (await groupResp.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        (await BlockViaApiAsync(ada, zoeId)).EnsureSuccessStatusCode();

        // Both still send and both still read.
        await SendAsync(zoe, groupId, "from zoe");
        await SendAsync(ada, groupId, "from ada");

        var forAda = await GetMessagesAsync(ada, groupId);
        Assert.Contains(forAda.GetProperty("items").EnumerateArray(), m => m.GetProperty("body").GetString() == "from zoe");

        var forZoe = await GetMessagesAsync(zoe, groupId);
        Assert.Contains(forZoe.GetProperty("items").EnumerateArray(), m => m.GetProperty("body").GetString() == "from ada");

        // The group is still in both inboxes.
        Assert.Contains((await GetInboxAsync(ada)).GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == groupId);
        Assert.Contains((await GetInboxAsync(zoe)).GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == groupId);
    }

    /// <summary>FR-030: unblocking restores messaging, with the prior history intact.</summary>
    [Fact]
    public async Task Unblocking_restores_messaging_and_the_history_survives()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (zoe, zoeId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(zoe, adaId);
        await SendAsync(zoe, conversationId, "original message");

        (await BlockViaApiAsync(ada, zoeId)).EnsureSuccessStatusCode();
        var unblock = await ada.DeleteAsync($"/api/v1/chat/blocks/{zoeId}");
        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);

        // Messaging works again…
        await SendAsync(zoe, conversationId, "back again");

        // …and the old history is still there.
        var page = await GetMessagesAsync(ada, conversationId);
        var bodies = page.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("body").GetString()).ToList();
        Assert.Contains("original message", bodies);
        Assert.Contains("back again", bodies);
    }

    [Fact]
    public async Task Blocking_is_idempotent_and_self_block_is_rejected()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, zoeId, _) = await NewUserAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await BlockViaApiAsync(ada, zoeId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await BlockViaApiAsync(ada, zoeId)).StatusCode);

        var list = await ada.GetFromJsonAsync<JsonElement>("/api/v1/chat/blocks", Json);
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await BlockViaApiAsync(ada, adaId)).StatusCode);
    }

    /// <summary>FR-033: a blocked player is not offered as a chat target.</summary>
    [Fact]
    public async Task A_blocked_player_is_absent_from_people_search()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, zoeId, zoeHandle) = await NewUserAsync();

        var before = await ada.GetFromJsonAsync<JsonElement>($"/api/v1/chat/search?q={zoeHandle}", Json);
        Assert.Contains(before.GetProperty("people").GetProperty("items").EnumerateArray(),
            p => p.GetProperty("userId").GetGuid() == zoeId);

        (await BlockViaApiAsync(ada, zoeId)).EnsureSuccessStatusCode();

        var after = await ada.GetFromJsonAsync<JsonElement>($"/api/v1/chat/search?q={zoeHandle}", Json);
        Assert.DoesNotContain(after.GetProperty("people").GetProperty("items").EnumerateArray(),
            p => p.GetProperty("userId").GetGuid() == zoeId);
    }

    // --- Mute / hide ------------------------------------------------------------

    /// <summary>FR-028: muted stops the badge but the row still lives and updates.</summary>
    [Fact]
    public async Task Muting_stops_the_badge_but_keeps_the_row()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, "one");
        Assert.Equal(1, await GetUnreadTotalAsync(ben));

        var mute = await ben.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/state", new { isMuted = true });
        Assert.Equal(HttpStatusCode.NoContent, mute.StatusCode);

        await SendAsync(ada, conversationId, "two");

        // Badge silent…
        Assert.Equal(0, await GetUnreadTotalAsync(ben));

        // …but the conversation is still listed and still current.
        var row = (await GetInboxAsync(ben)).GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conversationId);
        Assert.Equal("two", row.GetProperty("lastMessage").GetProperty("preview").GetString());
        Assert.True(row.GetProperty("isMuted").GetBoolean());
    }

    /// <summary>FR-029: hidden leaves the inbox.</summary>
    [Fact]
    public async Task Hiding_removes_the_conversation_from_the_inbox()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "one");

        await ben.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/state", new { isHidden = true });

        Assert.DoesNotContain((await GetInboxAsync(ben)).GetProperty("items").EnumerateArray(),
            c => c.GetProperty("id").GetGuid() == conversationId);
    }

    [Fact]
    public async Task Unmuting_brings_the_badge_back()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await ben.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/state", new { isMuted = true });
        await SendAsync(ada, conversationId, "one");
        Assert.Equal(0, await GetUnreadTotalAsync(ben));

        await ben.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/state", new { isMuted = false });
        Assert.Equal(1, await GetUnreadTotalAsync(ben));
    }

    [Fact]
    public async Task A_non_member_cannot_patch_state()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (mallory, _, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var resp = await mallory.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/state", new { isMuted = true });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
