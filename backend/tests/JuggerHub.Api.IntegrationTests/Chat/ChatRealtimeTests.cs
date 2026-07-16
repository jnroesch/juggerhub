using System.Net;
using System.Net.Http.Json;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// The realtime contract (feature 019, User Story 2) — asserted through the <c>IChatRealtime</c> seam
/// rather than a live socket.
/// </summary>
/// <remarks>
/// The two that matter most are <see cref="A_non_member_is_never_pushed_to"/> (FR-022 — the fan-out
/// audience is a security boundary, not a convenience) and
/// <see cref="Every_realtime_value_is_also_reachable_over_rest"/> (FR-023 — live is an enhancement,
/// never the source of truth).
/// </remarks>
[Collection("Chat")]
public sealed class ChatRealtimeTests : ChatTestSupport
{
    public ChatRealtimeTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Sending_pushes_the_message_to_the_other_member_and_not_the_sender()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        Factory.ChatRealtime.Clear();
        await SendAsync(ada, conversationId, "hello");

        var pushes = Factory.ChatRealtime.MessagesCreated;
        Assert.Single(pushes);
        Assert.Equal(conversationId, pushes[0].ConversationId);
        Assert.Equal(new[] { benId }, pushes[0].Recipients);

        // The sender already has the message in their HTTP response; pushing it back would double it.
        Assert.DoesNotContain(adaId, pushes.SelectMany(p => p.Recipients));
    }

    /// <summary>
    /// FR-022. The audience is resolved server-side from membership; a non-member must never appear in
    /// it. This is the socket-side twin of the 404 rule on REST.
    /// </summary>
    [Fact]
    public async Task A_non_member_is_never_pushed_to()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, malloryId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);

        Factory.ChatRealtime.Clear();
        await SendAsync(ada, conversationId, "private");

        Assert.DoesNotContain(malloryId, Factory.ChatRealtime.AllRecipients);
    }

    [Fact]
    public async Task A_recipient_gets_their_new_unread_total_pushed()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        Factory.ChatRealtime.Clear();
        await SendAsync(ada, conversationId, "one");

        var forBen = Factory.ChatRealtime.UnreadCounts.Where(u => u.RecipientUserId == benId).ToList();
        Assert.NotEmpty(forBen);
        Assert.Equal(1, forBen[^1].Count);
    }

    /// <summary>FR-016: reading converges the badge on the reader's own other tabs — and nobody else's.</summary>
    [Fact]
    public async Task Reading_pushes_the_new_total_to_the_reader_only()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        var messageId = await SendAsync(ada, conversationId, "one");

        Factory.ChatRealtime.Clear();
        await MarkReadAsync(ben, conversationId, messageId);

        var pushes = Factory.ChatRealtime.UnreadCounts;
        Assert.NotEmpty(pushes);
        Assert.All(pushes, p => Assert.Equal(benId, p.RecipientUserId));
        Assert.Equal(0, pushes[^1].Count);
        Assert.DoesNotContain(adaId, pushes.Select(p => p.RecipientUserId));
    }

    [Fact]
    public async Task Deleting_pushes_a_deletion_to_everyone_including_the_sender()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        var messageId = await SendAsync(ada, conversationId, "oops");

        Factory.ChatRealtime.Clear();
        (await ada.DeleteAsync($"/api/v1/chat/messages/{messageId}")).EnsureSuccessStatusCode();

        var pushes = Factory.ChatRealtime.MessagesDeleted;
        Assert.Single(pushes);
        Assert.Equal(messageId, pushes[0].MessageId);

        // The sender's OTHER tabs need the tombstone too.
        Assert.Contains(adaId, pushes[0].Recipients);
        Assert.Contains(benId, pushes[0].Recipients);
    }

    // --- Typing -----------------------------------------------------------------

    [Fact]
    public async Task Typing_pushes_to_the_others_and_persists_nothing()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        Factory.ChatRealtime.Clear();
        var resp = await ada.PostAsync($"/api/v1/chat/conversations/{conversationId}/typing", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var typings = Factory.ChatRealtime.Typings;
        Assert.Single(typings);
        Assert.Equal(new[] { benId }, typings[0].Recipients);
        Assert.Equal(adaId, typings[0].TypistUserId);
        Assert.NotEmpty(typings[0].DisplayName);

        // Nothing was written: the history is still empty.
        var page = await GetMessagesAsync(ada, conversationId);
        Assert.Empty(page.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task A_non_member_cannot_signal_typing()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (mallory, _, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        Factory.ChatRealtime.Clear();
        var resp = await mallory.PostAsync($"/api/v1/chat/conversations/{conversationId}/typing", null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Empty(Factory.ChatRealtime.Typings);
    }

    /// <summary>
    /// <b>FR-023 / SC-011 — the guarantee the whole design rests on.</b> Realtime is an enhancement
    /// layered over durable storage: every value the socket carries is also reachable on a normal REST
    /// load. Here the pushes are thrown away entirely (a dead socket), and REST still returns the full
    /// truth — so a player with no connection is stale, never wrong.
    /// </summary>
    [Fact]
    public async Task Every_realtime_value_is_also_reachable_over_rest()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var first = await SendAsync(ada, conversationId, "one");
        var second = await SendAsync(ada, conversationId, "two");

        // Pretend Ben was never connected: discard everything the socket would have delivered.
        Factory.ChatRealtime.Clear();

        // History — complete.
        var page = await GetMessagesAsync(ben, conversationId);
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());

        // Unread — correct.
        Assert.Equal(2, await GetUnreadTotalAsync(ben));

        // Inbox row — correct.
        var inbox = await GetInboxAsync(ben);
        var row = inbox.GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conversationId);
        Assert.Equal("two", row.GetProperty("lastMessage").GetProperty("preview").GetString());

        // Read state — settles correctly over REST alone.
        await MarkReadAsync(ben, conversationId, second);
        Assert.Equal(0, await GetUnreadTotalAsync(ben));

        var forAda = await GetMessagesAsync(ada, conversationId);
        Assert.Equal("Read", forAda.GetProperty("items")[0].GetProperty("readState").GetString());
        Assert.NotEqual(Guid.Empty, first);
    }

    /// <summary>
    /// A push must never be able to fail a send. The message is durable before the fan-out runs, so a
    /// realtime problem degrades liveness, never data.
    /// </summary>
    [Fact]
    public async Task The_message_is_persisted_before_any_push_happens()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        Factory.ChatRealtime.Clear();
        var messageId = await SendAsync(ada, conversationId, "durable first");

        // The push carries the message that is already in the store — same id, not a preview of one.
        var pushed = Factory.ChatRealtime.MessagesCreated.Single();
        Assert.Equal(messageId, pushed.Message.Id);

        var page = await GetMessagesAsync(ada, conversationId);
        Assert.Equal(messageId, page.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    /// <summary>
    /// The pushed projection is per recipient: <c>isOwn</c> differs by viewer, so pushing the sender's
    /// projection to everyone would render the recipient's own message on the wrong side.
    /// </summary>
    [Fact]
    public async Task The_pushed_message_is_projected_for_the_recipient_not_the_sender()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        Factory.ChatRealtime.Clear();
        await SendAsync(ada, conversationId, "from ada");

        var pushed = Factory.ChatRealtime.MessagesCreated.Single();
        Assert.Equal(new[] { benId }, pushed.Recipients);
        Assert.False(pushed.Message.IsOwn);
        Assert.NotNull(pushed.Message.SenderName);
    }
}
