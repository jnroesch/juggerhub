using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Sending and reading messages (feature 019, User Story 1).
/// </summary>
[Collection("Chat")]
public sealed class ChatMessageTests : ChatTestSupport
{
    public ChatMessageTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task A_sent_message_is_readable_by_both_players()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "you coming to training tonight?");

        var forBen = await GetMessagesAsync(ben, conversationId);
        var message = forBen.GetProperty("items").EnumerateArray().Single();

        Assert.Equal("you coming to training tonight?", message.GetProperty("body").GetString());
        Assert.False(message.GetProperty("isOwn").GetBoolean());
    }

    [Fact]
    public async Task Own_and_others_messages_are_distinguished_per_viewer()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "from ada");

        var forAda = await GetMessagesAsync(ada, conversationId);
        Assert.True(forAda.GetProperty("items")[0].GetProperty("isOwn").GetBoolean());

        var forBen = await GetMessagesAsync(ben, conversationId);
        Assert.False(forBen.GetProperty("items")[0].GetProperty("isOwn").GetBoolean());
    }

    /// <summary>FR-010: empty, whitespace-only and over-length bodies are rejected server-side.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t  \n")]
    public async Task Empty_messages_are_rejected(string body)
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var resp = await ada.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages", new { body });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task An_over_length_message_is_rejected()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var resp = await ada.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages",
            new { body = new string('x', 2001) });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task A_non_member_cannot_send_and_gets_404()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (mallory, _, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);

        var resp = await mallory.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages",
            new { body = "let me in" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    /// <summary>
    /// FR-011: ordering is stable, identical for every viewer, and derived from the server's UUIDv7
    /// ids — never a client clock. Sending in a tight loop is the closest we get to "the same tick".
    /// </summary>
    [Fact]
    public async Task Message_order_is_identical_for_both_viewers_even_when_sent_rapidly()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var sent = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            sent.Add(await SendAsync(i % 2 == 0 ? ada : ben, conversationId, $"m{i}"));
        }

        var forAda = OrderedIds(await GetMessagesAsync(ada, conversationId));
        var forBen = OrderedIds(await GetMessagesAsync(ben, conversationId));

        Assert.Equal(forAda, forBen);

        // Newest first, and exactly the ids we sent, in reverse send order.
        sent.Reverse();
        Assert.Equal(sent, forAda);
    }

    /// <summary>Keyset paging walks the whole history with no gap and no repeat.</summary>
    [Fact]
    public async Task Keyset_paging_returns_every_message_exactly_once()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var sent = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            sent.Add(await SendAsync(ada, conversationId, $"m{i}"));
        }

        var seen = new List<Guid>();
        Guid? before = null;
        for (var guard = 0; guard < 10; guard++)
        {
            var url = $"/api/v1/chat/conversations/{conversationId}/messages?take=5"
                + (before is { } b ? $"&before={b}" : string.Empty);
            var resp = await ada.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var page = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);

            seen.AddRange(OrderedIds(page));

            if (page.TryGetProperty("nextBefore", out var next) && next.ValueKind != JsonValueKind.Null)
            {
                before = next.GetGuid();
            }
            else
            {
                break;
            }
        }

        Assert.Equal(sent.Count, seen.Count);
        Assert.Equal(sent.Count, seen.Distinct().Count());
        Assert.Equal(sent.OrderBy(x => x), seen.OrderBy(x => x));
    }

    /// <summary>
    /// FR-014: a body is stored and returned verbatim as text. A chat is the natural home for stored
    /// XSS, so the API must not sanitise, escape or otherwise interpret it — the client binds it as
    /// text. This test pins the round-trip so nobody later "helpfully" adds HTML handling.
    /// </summary>
    [Fact]
    public async Task Markup_in_a_body_round_trips_as_literal_text()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        const string payload = "<script>alert('xss')</script> & <b>bold</b>";
        await SendAsync(ada, conversationId, payload);

        var forBen = await GetMessagesAsync(ben, conversationId);
        Assert.Equal(payload, forBen.GetProperty("items")[0].GetProperty("body").GetString());
    }

    [Fact]
    public async Task Sending_moves_the_conversation_to_the_top_of_the_inbox()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();

        var withBen = await StartDirectAsync(ada, benId);
        var withNia = await StartDirectAsync(ada, niaId);

        await SendAsync(ada, withNia, "hi nia");
        await SendAsync(ada, withBen, "hi ben");

        var inbox = await GetInboxAsync(ada);
        Assert.Equal(withBen, inbox.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task The_inbox_preview_shows_the_last_message()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, "first");
        await SendAsync(ada, conversationId, "grabbing the chain, omw");

        var inbox = await GetInboxAsync(ada);
        var row = inbox.GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conversationId);

        Assert.Equal("grabbing the chain, omw", row.GetProperty("lastMessage").GetProperty("preview").GetString());
    }

    private static List<Guid> OrderedIds(JsonElement page) =>
        page.GetProperty("items").EnumerateArray()
            .Select(m => m.GetProperty("id").GetGuid())
            .ToList();
}
