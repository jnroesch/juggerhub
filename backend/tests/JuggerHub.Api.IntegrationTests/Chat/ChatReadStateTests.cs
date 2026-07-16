using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Unread counts and read receipts (feature 019, User Story 1 / SC-008). These all lean on the read
/// marker being a single <c>LastReadMessageId</c> compared against UUIDv7 message ids.
/// </summary>
[Collection("Chat")]
public sealed class ChatReadStateTests : ChatTestSupport
{
    public ChatReadStateTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Unread_rises_on_receive_and_clears_on_read()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        Assert.Equal(0, await GetUnreadTotalAsync(ben));

        await SendAsync(ada, conversationId, "one");
        var last = await SendAsync(ada, conversationId, "two");

        Assert.Equal(2, await GetUnreadTotalAsync(ben));

        await MarkReadAsync(ben, conversationId, last);

        Assert.Equal(0, await GetUnreadTotalAsync(ben));
    }

    [Fact]
    public async Task Your_own_messages_never_count_as_unread_for_you()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, "talking to myself");

        Assert.Equal(0, await GetUnreadTotalAsync(ada));
    }

    [Fact]
    public async Task The_inbox_row_carries_its_own_unread_count()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, "one");
        await SendAsync(ada, conversationId, "two");

        var inbox = await GetInboxAsync(ben);
        var row = inbox.GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conversationId);

        Assert.Equal(2, row.GetProperty("unreadCount").GetInt32());
    }

    /// <summary>
    /// The marker never moves backwards. A slow request from a stale tab must not resurrect messages
    /// the player already read elsewhere — otherwise the badge would flap between devices.
    /// </summary>
    [Fact]
    public async Task Marking_read_never_moves_the_marker_backwards()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var first = await SendAsync(ada, conversationId, "one");
        var second = await SendAsync(ada, conversationId, "two");

        await MarkReadAsync(ben, conversationId, second);
        Assert.Equal(0, await GetUnreadTotalAsync(ben));

        // A stale tab reports it only read as far as the FIRST message.
        await MarkReadAsync(ben, conversationId, first);

        Assert.Equal(0, await GetUnreadTotalAsync(ben));
    }

    /// <summary>
    /// The marker must name a real message in this conversation. Otherwise a client could park it
    /// beyond every future id and never show an unread again — client-supplied state deciding server
    /// truth is exactly what the constitution forbids.
    /// </summary>
    [Fact]
    public async Task Marking_read_with_a_foreign_message_id_is_rejected()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();

        var withBen = await StartDirectAsync(ada, benId);
        var withNia = await StartDirectAsync(ada, niaId);
        var niaMessage = await SendAsync(ada, withNia, "different conversation");
        await SendAsync(ada, withBen, "unread");

        var resp = await ben.PostAsJsonAsync($"/api/v1/chat/conversations/{withBen}/read",
            new { lastReadMessageId = niaMessage });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal(1, await GetUnreadTotalAsync(ben));
    }

    [Fact]
    public async Task A_non_member_cannot_mark_read()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (mallory, _, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        var messageId = await SendAsync(ada, conversationId, "hi");

        var resp = await mallory.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/read",
            new { lastReadMessageId = messageId });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    /// <summary>FR-017: Sent flips to Read only when the OTHER participant reads it.</summary>
    [Fact]
    public async Task A_dm_read_receipt_flips_from_sent_to_read()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var messageId = await SendAsync(ada, conversationId, "nice — i'll warm up the runners");

        var before = await GetMessagesAsync(ada, conversationId);
        Assert.Equal("Sent", before.GetProperty("items")[0].GetProperty("readState").GetString());

        await MarkReadAsync(ben, conversationId, messageId);

        var after = await GetMessagesAsync(ada, conversationId);
        Assert.Equal("Read", after.GetProperty("items")[0].GetProperty("readState").GetString());
    }

    /// <summary>The sender reading their own message does not mark it read — only the recipient can.</summary>
    [Fact]
    public async Task Reading_your_own_message_does_not_flip_your_receipt_to_read()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var messageId = await SendAsync(ada, conversationId, "hello?");
        await MarkReadAsync(ada, conversationId, messageId);

        var forAda = await GetMessagesAsync(ada, conversationId);
        Assert.Equal("Sent", forAda.GetProperty("items")[0].GetProperty("readState").GetString());
    }

    /// <summary>A recipient sees no receipt on someone else's message — receipts are for the sender.</summary>
    [Fact]
    public async Task A_recipient_sees_no_read_state_on_the_senders_message()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, "hi");

        var forBen = await GetMessagesAsync(ben, conversationId);
        Assert.Equal(JsonValueKind.Null, forBen.GetProperty("items")[0].GetProperty("readState").ValueKind);
    }

    /// <summary>Edge case: a deleted message stops counting, and unread never goes negative.</summary>
    [Fact]
    public async Task Deleting_a_counted_message_leaves_unread_correct()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var first = await SendAsync(ada, conversationId, "one");
        await SendAsync(ada, conversationId, "two");
        Assert.Equal(2, await GetUnreadTotalAsync(ben));

        var del = await ada.DeleteAsync($"/api/v1/chat/messages/{first}");
        del.EnsureSuccessStatusCode();

        var unread = await GetUnreadTotalAsync(ben);
        Assert.Equal(1, unread);
        Assert.True(unread >= 0);
    }

    /// <summary>Edge case: a conversation with no messages shows no badge and does not inflate the total.</summary>
    [Fact]
    public async Task An_empty_conversation_contributes_no_unread()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        await StartDirectAsync(ada, benId);

        Assert.Equal(0, await GetUnreadTotalAsync(ben));
        Assert.Equal(0, await GetUnreadTotalAsync(ada));
    }
}
