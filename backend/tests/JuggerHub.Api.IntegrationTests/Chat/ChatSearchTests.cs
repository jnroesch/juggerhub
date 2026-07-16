using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Chat search (feature 019, User Story 6).
/// </summary>
/// <remarks>
/// <see cref="A_term_only_in_someone_elses_conversation_returns_nothing"/> is SC-006 and the reason
/// the scope predicate lives inside the query rather than in a post-filter.
/// </remarks>
[Collection("Chat")]
public sealed class ChatSearchTests : ChatTestSupport
{
    public ChatSearchTests(JuggerHubApiFactory factory) : base(factory) { }

    private static async Task<JsonElement> SearchAsync(HttpClient client, string q) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/v1/chat/search?q={Uri.EscapeDataString(q)}", Json);

    [Fact]
    public async Task Finds_a_message_in_your_own_conversation()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var needle = "pompfen" + Guid.NewGuid().ToString("N")[..6];
        await SendAsync(ada, conversationId, $"who's bringing {needle} on saturday?");

        var results = await SearchAsync(ada, needle);
        var hits = results.GetProperty("messages").GetProperty("items").EnumerateArray().ToList();

        Assert.Single(hits);
        Assert.Equal(conversationId, hits[0].GetProperty("conversationId").GetGuid());
        Assert.Contains(needle, hits[0].GetProperty("snippet").GetString());
    }

    /// <summary>
    /// <b>SC-006 / FR-035.</b> Driving the API directly: a term that exists only in a conversation the
    /// searcher is not in returns zero results — and no count that would hint it exists.
    /// </summary>
    [Fact]
    public async Task A_term_only_in_someone_elses_conversation_returns_nothing()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (mallory, _, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        var secret = "secret" + Guid.NewGuid().ToString("N")[..8];
        await SendAsync(ada, conversationId, $"the code is {secret}");

        // Ada finds it.
        var forAda = await SearchAsync(ada, secret);
        Assert.Equal(1, forAda.GetProperty("messages").GetProperty("totalCount").GetInt32());

        // Mallory does not — no items, and no count leaking its existence.
        var forMallory = await SearchAsync(mallory, secret);
        Assert.Empty(forMallory.GetProperty("messages").GetProperty("items").EnumerateArray());
        Assert.Equal(0, forMallory.GetProperty("messages").GetProperty("totalCount").GetInt32());
    }

    /// <summary>A player who left a group stops finding its messages.</summary>
    [Fact]
    public async Task Leaving_a_group_removes_its_messages_from_your_search()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();

        var resp = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { benId, niaId }, name = "Weekend crew" });
        var groupId = (await resp.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var needle = "carpool" + Guid.NewGuid().ToString("N")[..6];
        await SendAsync(ada, groupId, $"{needle} leaves at 8");

        Assert.Equal(1, (await SearchAsync(ben, needle)).GetProperty("messages").GetProperty("totalCount").GetInt32());

        await ben.DeleteAsync($"/api/v1/chat/conversations/{groupId}/members/me");

        Assert.Equal(0, (await SearchAsync(ben, needle)).GetProperty("messages").GetProperty("totalCount").GetInt32());
    }

    /// <summary>FR-050c: a deleted message stops matching.</summary>
    [Fact]
    public async Task A_deleted_message_never_matches()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var needle = "regret" + Guid.NewGuid().ToString("N")[..6];
        var messageId = await SendAsync(ada, conversationId, $"something {needle}");

        Assert.Equal(1, (await SearchAsync(ada, needle)).GetProperty("messages").GetProperty("totalCount").GetInt32());

        await ada.DeleteAsync($"/api/v1/chat/messages/{messageId}");

        Assert.Equal(0, (await SearchAsync(ada, needle)).GetProperty("messages").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Finds_people_and_surfaces_an_existing_dm()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, benHandle) = await NewUserAsync();

        var before = await SearchAsync(ada, benHandle);
        var hit = before.GetProperty("people").GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("userId").GetGuid() == benId);
        Assert.Equal(JsonValueKind.Null, hit.GetProperty("existingConversationId").ValueKind);

        var conversationId = await StartDirectAsync(ada, benId);

        var after = await SearchAsync(ada, benHandle);
        var hit2 = after.GetProperty("people").GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("userId").GetGuid() == benId);
        Assert.Equal(conversationId, hit2.GetProperty("existingConversationId").GetGuid());
    }

    /// <summary>FR-049: people search reaches everyone — it is not restricted to teammates.</summary>
    [Fact]
    public async Task People_search_reaches_players_you_share_nothing_with()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, zoeId, zoeHandle) = await NewUserAsync();

        var results = await SearchAsync(ada, zoeHandle);

        Assert.Contains(results.GetProperty("people").GetProperty("items").EnumerateArray(),
            p => p.GetProperty("userId").GetGuid() == zoeId);
    }

    [Fact]
    public async Task You_never_appear_in_your_own_people_search()
    {
        var (ada, adaId, adaHandle) = await NewUserAsync();

        var results = await SearchAsync(ada, adaHandle);

        Assert.DoesNotContain(results.GetProperty("people").GetProperty("items").EnumerateArray(),
            p => p.GetProperty("userId").GetGuid() == adaId);
    }

    [Fact]
    public async Task A_short_or_empty_term_returns_an_empty_result_not_an_error()
    {
        var (ada, _, _) = await NewUserAsync();

        foreach (var q in new[] { "", "a" })
        {
            var results = await SearchAsync(ada, q);
            Assert.Empty(results.GetProperty("messages").GetProperty("items").EnumerateArray());
            Assert.Empty(results.GetProperty("people").GetProperty("items").EnumerateArray());
        }
    }

    /// <summary>SC-010: search is bounded like every other list.</summary>
    [Fact]
    public async Task Search_results_are_paginated()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var needle = "chain" + Guid.NewGuid().ToString("N")[..6];
        for (var i = 0; i < 5; i++)
        {
            await SendAsync(ada, conversationId, $"{needle} number {i}");
        }

        var page = await ada.GetFromJsonAsync<JsonElement>(
            $"/api/v1/chat/search?q={needle}&skip=0&take=2", Json);

        Assert.Equal(2, page.GetProperty("messages").GetProperty("items").GetArrayLength());
        Assert.Equal(5, page.GetProperty("messages").GetProperty("totalCount").GetInt32());
    }

    /// <summary>Accent-insensitive, matching feature 007's convention (research §6).</summary>
    [Fact]
    public async Task Search_is_accent_insensitive()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var token = Guid.NewGuid().ToString("N")[..6];
        await SendAsync(ada, conversationId, $"training in Köln {token}");

        var results = await SearchAsync(ada, $"Koln {token}");
        Assert.Equal(1, results.GetProperty("messages").GetProperty("totalCount").GetInt32());
    }
}
