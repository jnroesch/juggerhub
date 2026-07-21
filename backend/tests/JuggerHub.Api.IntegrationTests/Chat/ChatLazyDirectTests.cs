using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Lazy direct-message creation (feature 022): a direct conversation comes into existence only when
/// the first message is sent. Opening a chat and leaving without sending leaves nothing behind.
/// </summary>
[Collection("Chat")]
public sealed class ChatLazyDirectTests : ChatTestSupport
{
    public ChatLazyDirectTests(JuggerHubApiFactory factory) : base(factory) { }

    private static Task<HttpResponseMessage> SendDirectAsync(HttpClient client, Guid targetUserId, string body) =>
        client.PostAsJsonAsync($"/api/v1/chat/direct/{targetUserId}/messages", new { body });

    private async Task<int> DirectConversationCountAsync(Guid a, Guid b)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = Conversation.BuildDirectPairKey(a, b);
        return await db.Conversations.CountAsync(c => c.DirectPairKey == key);
    }

    private static IEnumerable<string?> InboxIds(JsonElement inbox) =>
        inbox.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString());

    [Fact]
    public async Task No_conversation_exists_until_a_first_message_is_sent()
    {
        // The compose view is client-only — there is no server "open". A fresh pair therefore has no
        // conversation and an empty inbox until a message is actually sent (FR-001 / FR-009).
        var (alice, aliceId, _) = await NewUserAsync();
        var (_, bobId, _) = await NewUserAsync();

        Assert.Empty(InboxIds(await GetInboxAsync(alice)));
        Assert.Equal(0, await DirectConversationCountAsync(aliceId, bobId));
    }

    [Fact]
    public async Task First_send_creates_the_conversation_and_delivers_to_both()
    {
        var (alice, aliceId, _) = await NewUserAsync();
        var (bob, bobId, _) = await NewUserAsync();

        Assert.Equal(0, await DirectConversationCountAsync(aliceId, bobId));

        var resp = await SendDirectAsync(alice, bobId, "hey bob");
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        var conversationId = payload.GetProperty("conversationId").GetGuid().ToString();
        Assert.Equal("hey bob", payload.GetProperty("message").GetProperty("body").GetString());

        Assert.Equal(1, await DirectConversationCountAsync(aliceId, bobId));
        Assert.Contains(conversationId, InboxIds(await GetInboxAsync(alice)));
        Assert.Contains(conversationId, InboxIds(await GetInboxAsync(bob)));
    }

    [Fact]
    public async Task An_existing_conversation_is_reused_without_a_duplicate()
    {
        var (alice, aliceId, _) = await NewUserAsync();
        var (_, bobId, _) = await NewUserAsync();

        (await SendDirectAsync(alice, bobId, "one")).EnsureSuccessStatusCode();
        (await SendDirectAsync(alice, bobId, "two")).EnsureSuccessStatusCode();

        Assert.Equal(1, await DirectConversationCountAsync(aliceId, bobId));
    }

    [Fact]
    public async Task A_blocked_first_send_is_refused_and_creates_nothing()
    {
        var (alice, aliceId, _) = await NewUserAsync();
        var (_, bobId, _) = await NewUserAsync();
        await BlockAsync(bobId, aliceId); // bob blocked alice — the block holds in both directions

        var resp = await SendDirectAsync(alice, bobId, "hi");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal(0, await DirectConversationCountAsync(aliceId, bobId));
    }

    [Fact]
    public async Task Concurrent_first_sends_resolve_to_one_conversation()
    {
        var (alice, aliceId, _) = await NewUserAsync();
        var (_, bobId, _) = await NewUserAsync();

        // Several first sends at once — the unique DirectPairKey must collapse them to one conversation
        // rather than making duplicates (FR-006 / SC-003).
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(i => SendDirectAsync(alice, bobId, $"msg {i}")));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, $"status {(int)r.StatusCode}"));
        Assert.Equal(1, await DirectConversationCountAsync(aliceId, bobId));
    }
}
