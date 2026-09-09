using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// People search (feature 019, User Story 6, narrowed by feature 046): the half of
/// <c>/chat/search</c> that the new-chat picker, compose-by-handle and the profile Message action
/// depend on. Message-text search no longer exists — <see cref="The_response_carries_no_messages_property"/>
/// is the 046 FR-010 evidence, and <c>ChatInboxSearchTests</c> covers what replaced it.
/// </summary>
[Collection("Chat")]
public sealed class ChatSearchTests : ChatTestSupport
{
    public ChatSearchTests(JuggerHubApiFactory factory) : base(factory) { }

    private static async Task<JsonElement> SearchAsync(HttpClient client, string q) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/v1/chat/search?q={Uri.EscapeDataString(q)}", Json);

    private static string Token() => Guid.NewGuid().ToString("N")[..6];

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
            Assert.Empty(results.GetProperty("people").GetProperty("items").EnumerateArray());
            Assert.False(results.TryGetProperty("messages", out _));
        }
    }

    /// <summary>
    /// <b>046 FR-010.</b> Message-text search is removed, not hidden: the response has no
    /// <c>messages</c> property at all, so no client — and no future refactor — can read one.
    /// </summary>
    [Fact]
    public async Task The_response_carries_no_messages_property()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, benHandle) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        var needle = "pompfen" + Token();
        await SendAsync(ada, conversationId, $"who's bringing {needle} on saturday?");

        var byMessageText = await SearchAsync(ada, needle);
        Assert.False(byMessageText.TryGetProperty("messages", out _));
        Assert.Empty(byMessageText.GetProperty("people").GetProperty("items").EnumerateArray());

        var byName = await SearchAsync(ada, benHandle);
        Assert.False(byName.TryGetProperty("messages", out _));
        Assert.Single(byName.GetProperty("people").GetProperty("items").EnumerateArray());
    }

    /// <summary>SC-010: search is bounded like every other list.</summary>
    [Fact]
    public async Task Search_results_are_paginated()
    {
        var (ada, _, _) = await NewUserAsync();
        var token = Token();
        for (var i = 0; i < 5; i++)
        {
            var (_, userId, _) = await NewUserAsync();
            await SetDisplayNameAsync(userId, $"Pager {token} {i}");
        }

        var page = await ada.GetFromJsonAsync<JsonElement>(
            $"/api/v1/chat/search?q={token}&skip=0&take=2", Json);

        Assert.Equal(2, page.GetProperty("people").GetProperty("items").GetArrayLength());
        Assert.Equal(5, page.GetProperty("people").GetProperty("totalCount").GetInt32());
    }

    /// <summary>Accent-insensitive, matching feature 007's convention (research §6).</summary>
    [Fact]
    public async Task Search_is_accent_insensitive()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var token = Token();
        await SetDisplayNameAsync(benId, $"Köln {token}");

        var results = await SearchAsync(ada, $"Koln {token}");

        Assert.Contains(results.GetProperty("people").GetProperty("items").EnumerateArray(),
            p => p.GetProperty("userId").GetGuid() == benId);
    }
}
