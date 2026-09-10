using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Hiding a conversation is reversible (feature 048, GH #222). Hide means <em>archive</em> — tidy
/// away until something happens — and mute means "don't bother me"; each control has one job.
/// </summary>
/// <remarks>
/// <para>
/// Two of these carry behaviour that is invisible in the diff and would rot silently:
/// <see cref="The_nav_total_already_includes_a_returned_conversation"/> is the only thing that fails
/// if the flag is cleared <em>after</em> the unread totals are pushed rather than before (FR-009), and
/// <see cref="A_system_line_leaves_a_hidden_conversation_hidden"/> is what stops a future refactor
/// folding the clear into <c>WriteSystemMessageAsync</c> and quietly repealing FR-011.
/// </para>
/// </remarks>
[Collection("Chat")]
public sealed class ChatHideTests : ChatTestSupport
{
    public ChatHideTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- Helpers ----------------------------------------------------------------

    private static async Task SetStateAsync(HttpClient client, Guid conversationId, object patch)
    {
        var resp = await client.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/state", patch);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private static async Task<Guid[]> InboxIdsAsync(HttpClient client) =>
        (await GetInboxAsync(client)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())
            .ToArray();

    private static async Task<JsonElement> GetDetailAsync(HttpClient client, Guid conversationId)
    {
        var resp = await client.GetAsync($"/api/v1/chat/conversations/{conversationId}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static async Task<Guid> CreateGroupAsync(HttpClient client, string name, params Guid[] members)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = members, name });
        Assert.True(resp.IsSuccessStatusCode, $"group failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return (await resp.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private static async Task AddMembersAsync(HttpClient client, Guid conversationId, params Guid[] userIds)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/members", new { userIds });
        Assert.True(resp.IsSuccessStatusCode, $"add members failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    private static async Task LeaveAsync(HttpClient client, Guid conversationId)
    {
        var resp = await client.DeleteAsync($"/api/v1/chat/conversations/{conversationId}/members/me");
        Assert.True(resp.IsSuccessStatusCode, $"leave failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    /// <summary>Read the stored flag directly — the only way to observe a non-member's own state.</summary>
    private async Task<bool?> StoredIsHiddenAsync(Guid conversationId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ConversationParticipants.AsNoTracking()
            .Where(p => p.ConversationId == conversationId && p.UserId == userId)
            .Select(p => (bool?)p.IsHidden)
            .FirstOrDefaultAsync();
    }

    // --- The explicit control (US2 / C1, C2, C3) --------------------------------

    /// <summary>C1 — FR-001. The API has always accepted this; until feature 048 nothing ever sent it.</summary>
    [Fact]
    public async Task Un_hiding_returns_the_conversation_to_the_inbox()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ada, conversationId, new { isHidden = true });
        Assert.DoesNotContain(conversationId, await InboxIdsAsync(ada));

        await SetStateAsync(ada, conversationId, new { isHidden = false });

        Assert.Contains(conversationId, await InboxIdsAsync(ada));
        Assert.False((await GetDetailAsync(ada, conversationId)).GetProperty("isHidden").GetBoolean());
    }

    /// <summary>C2 — un-hiding something that was never hidden is the state you asked for, not an error.</summary>
    [Fact]
    public async Task Un_hiding_a_conversation_that_was_never_hidden_is_a_no_op()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ada, conversationId, new { isHidden = false });

        Assert.Contains(conversationId, await InboxIdsAsync(ada));
    }

    /// <summary>
    /// C3 — FR-005. Hide and mute are orthogonal: each control has one job, and all four
    /// combinations are reachable. This is the invariant the whole feature rests on.
    /// </summary>
    [Fact]
    public async Task Hide_and_mute_never_disturb_each_other()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ada, conversationId, new { isMuted = true });
        await SetStateAsync(ada, conversationId, new { isHidden = true });

        var bothOn = await GetDetailAsync(ada, conversationId);
        Assert.True(bothOn.GetProperty("isMuted").GetBoolean());
        Assert.True(bothOn.GetProperty("isHidden").GetBoolean());

        // Un-hiding must leave the mute exactly where it was.
        await SetStateAsync(ada, conversationId, new { isHidden = false });

        var afterUnhide = await GetDetailAsync(ada, conversationId);
        Assert.True(afterUnhide.GetProperty("isMuted").GetBoolean());
        Assert.False(afterUnhide.GetProperty("isHidden").GetBoolean());

        // And unmuting must leave the (now false) hidden state alone.
        await SetStateAsync(ada, conversationId, new { isMuted = false });

        var afterUnmute = await GetDetailAsync(ada, conversationId);
        Assert.False(afterUnmute.GetProperty("isMuted").GetBoolean());
        Assert.False(afterUnmute.GetProperty("isHidden").GetBoolean());
    }

    // --- The automatic return (US1 / C4, C5, C6, C8) ----------------------------

    /// <summary>C4 — FR-007. A message is "something happening": the archive opens again.</summary>
    [Fact]
    public async Task A_message_returns_a_hidden_conversation_to_the_recipients_inbox()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ben, conversationId, new { isHidden = true });
        Assert.DoesNotContain(conversationId, await InboxIdsAsync(ben));

        await SendAsync(ada, conversationId, "still there?");

        Assert.Contains(conversationId, await InboxIdsAsync(ben));
        Assert.False((await GetDetailAsync(ben, conversationId)).GetProperty("isHidden").GetBoolean());
    }

    /// <summary>
    /// C4 — FR-012. Before feature 048 you could reach a conversation you had hidden by its direct
    /// link and post into it, and the message would be invisible in your own inbox.
    /// </summary>
    [Fact]
    public async Task Your_own_message_returns_a_conversation_you_hid_yourself()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ada, conversationId, new { isHidden = true });
        Assert.DoesNotContain(conversationId, await InboxIdsAsync(ada));

        await SendAsync(ada, conversationId, "actually, one more thing");

        Assert.Contains(conversationId, await InboxIdsAsync(ada));
    }

    /// <summary>
    /// C5 — FR-009, and the reason the clear is placed where it is. <c>PushMessageToOthersAsync</c>
    /// recomputes each recipient's badge, and that total excludes hidden conversations — so a clear
    /// that runs after the push returns the conversation to the inbox carrying unread messages the
    /// navigation badge does not count.
    /// </summary>
    [Fact]
    public async Task The_nav_total_already_includes_a_returned_conversation()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");
        await SetStateAsync(ben, conversationId, new { isHidden = true });

        Assert.Equal(0, await GetUnreadTotalAsync(ben));

        Factory.ChatRealtime.Clear();
        await SendAsync(ada, conversationId, "knock knock");

        // The count pushed during the send — not a later re-query — is what a connected client acts on.
        var pushed = Factory.ChatRealtime.UnreadCounts.Where(u => u.RecipientUserId == benId).ToList();
        Assert.NotEmpty(pushed);
        Assert.True(pushed[^1].Count > 0,
            "the hidden flag must be cleared BEFORE the recipient's unread total is recomputed (FR-009)");

        Assert.True(await GetUnreadTotalAsync(ben) > 0);
    }

    /// <summary>C6 — FR-005. The conversation comes back; the mute is a separate choice and survives.</summary>
    [Fact]
    public async Task A_returned_conversation_that_is_also_muted_stays_out_of_the_nav_total()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ben, conversationId, new { isMuted = true });
        await SetStateAsync(ben, conversationId, new { isHidden = true });

        await SendAsync(ada, conversationId, "back you come");

        Assert.Contains(conversationId, await InboxIdsAsync(ben));
        Assert.Equal(0, await GetUnreadTotalAsync(ben));

        var detail = await GetDetailAsync(ben, conversationId);
        Assert.True(detail.GetProperty("isMuted").GetBoolean());
        Assert.False(detail.GetProperty("isHidden").GetBoolean());
    }

    /// <summary>C8 — FR-006. Hiding is a private preference; one member's choice is not another's.</summary>
    [Fact]
    public async Task Hiding_affects_only_the_player_who_did_it()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (cara, caraId, _) = await NewUserAsync();
        var conversationId = await CreateGroupAsync(ada, "Weekend crew", benId, caraId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ben, conversationId, new { isHidden = true });

        Assert.DoesNotContain(conversationId, await InboxIdsAsync(ben));
        Assert.Contains(conversationId, await InboxIdsAsync(cara));

        await SendAsync(ada, conversationId, "anyone about?");

        // The message returns it for Ben, and never removed it for Cara.
        Assert.Contains(conversationId, await InboxIdsAsync(ben));
        Assert.Contains(conversationId, await InboxIdsAsync(cara));
    }

    /// <summary>
    /// FR-011. A system line is not somebody writing. Driven through the real path that emits one —
    /// adding a member to a group — so this breaks if the clear is ever moved into
    /// <c>WriteSystemMessageAsync</c> or a helper the two methods share.
    /// </summary>
    [Fact]
    public async Task A_system_line_leaves_a_hidden_conversation_hidden()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, caraId, _) = await NewUserAsync();
        var (_, daveId, _) = await NewUserAsync();
        // Two others from the start: the create endpoint makes a direct conversation, not a group,
        // when only one other player is named — and a direct conversation has nobody to add.
        var conversationId = await CreateGroupAsync(ada, "Weekend crew", benId, caraId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ben, conversationId, new { isHidden = true });
        Assert.DoesNotContain(conversationId, await InboxIdsAsync(ben));

        // Writes a "joined" system line into the conversation.
        await AddMembersAsync(ada, conversationId, daveId);

        Assert.DoesNotContain(conversationId, await InboxIdsAsync(ben));
        Assert.True((await GetDetailAsync(ben, conversationId)).GetProperty("isHidden").GetBoolean());
    }

    /// <summary>
    /// The <c>LeftDate == null</c> clause. A group's leaver keeps their row so their past messages
    /// stay attributable — clearing its flag would silently un-archive the conversation for them if
    /// they ever rejoined.
    /// </summary>
    [Fact]
    public async Task A_message_does_not_clear_the_flag_of_someone_who_left_the_group()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, caraId, _) = await NewUserAsync();
        var conversationId = await CreateGroupAsync(ada, "Weekend crew", benId, caraId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ben, conversationId, new { isHidden = true });
        await LeaveAsync(ben, conversationId);

        await SendAsync(ada, conversationId, "carrying on without you");

        Assert.True(await StoredIsHiddenAsync(conversationId, benId));
    }

    /// <summary>
    /// C9 — FR-017. Hiding has never been an access control, and this feature does not make it one.
    /// </summary>
    [Fact]
    public async Task A_hidden_conversation_is_still_openable_by_its_id()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ben, conversationId, new { isHidden = true });

        var detail = await GetDetailAsync(ben, conversationId);
        Assert.Equal(conversationId, detail.GetProperty("id").GetGuid());
        Assert.True(detail.GetProperty("isHidden").GetBoolean());

        var messages = await GetMessagesAsync(ben, conversationId);
        Assert.NotEmpty(messages.GetProperty("items").EnumerateArray());
    }

    /// <summary>
    /// C10 — a block already keeps the direct conversation out of the blocker's inbox (019 FR-031)
    /// and stops the other player sending. The block rule wins over the return: there is no message
    /// that could arrive, and un-hiding does not put a blocked conversation back on the list.
    /// </summary>
    [Fact]
    public async Task A_blocked_direct_conversation_does_not_come_back()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        await SetStateAsync(ada, conversationId, new { isHidden = true });
        await BlockAsync(adaId, benId);

        await SetStateAsync(ada, conversationId, new { isHidden = false });

        Assert.DoesNotContain(conversationId, await InboxIdsAsync(ada));
    }
}
