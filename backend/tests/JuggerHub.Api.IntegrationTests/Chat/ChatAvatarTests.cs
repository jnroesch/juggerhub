using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Player avatars flow through every chat surface (issue #193): before this, every avatar field in the
/// chat API was hardcoded null, so chat showed a grey placeholder even for players whose avatar renders
/// everywhere else. The URL points at the same handle-keyed, visibility-gated endpoint the rest of the
/// app uses; a player with no avatar — and a banned/erased account — must still resolve to null so the
/// placeholder (and the placeholder name) stays in place.
/// </summary>
[Collection("Chat")]
public sealed class ChatAvatarTests : ChatTestSupport
{
    public ChatAvatarTests(JuggerHubApiFactory factory) : base(factory) { }

    private static string ExpectedUrl(string handle) => $"/api/v1/profiles/{handle}/avatar";

    [Fact]
    public async Task Direct_inbox_row_carries_the_partners_avatar_url_when_they_have_one()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, benHandle) = await NewUserAsync();
        await SeedAvatarAsync(benId);

        var conversationId = await StartDirectAsync(ada, benId);

        var inbox = await GetInboxAsync(ada);
        var row = inbox.GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conversationId);

        Assert.Equal("User", row.GetProperty("avatar").GetProperty("kind").GetString());
        Assert.Equal(ExpectedUrl(benHandle), row.GetProperty("avatar").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Direct_inbox_row_avatar_url_is_null_when_the_partner_has_no_avatar()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);

        var inbox = await GetInboxAsync(ada);
        var row = inbox.GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conversationId);

        // A URL built for an avatar that does not exist would 404 into a broken image; null keeps the
        // placeholder in place instead.
        Assert.Null(row.GetProperty("avatar").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Conversation_detail_header_carries_the_partners_avatar_url()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, benHandle) = await NewUserAsync();
        await SeedAvatarAsync(benId);

        var conversationId = await StartDirectAsync(ada, benId);

        var detail = await ada.GetFromJsonAsync<JsonElement>(
            $"/api/v1/chat/conversations/{conversationId}", Json);

        Assert.Equal(ExpectedUrl(benHandle), detail.GetProperty("avatar").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Member_list_carries_each_members_avatar_url()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, benHandle) = await NewUserAsync();
        await SeedAvatarAsync(benId);

        var conversationId = await SeedConversationAsync(ConversationKind.Group, null, adaId, benId);

        var members = await ada.GetFromJsonAsync<JsonElement>(
            $"/api/v1/chat/conversations/{conversationId}/members", Json);
        var ben = members.GetProperty("items").EnumerateArray()
            .Single(m => m.GetProperty("userId").GetGuid() == benId);

        Assert.Equal(ExpectedUrl(benHandle), ben.GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task People_search_carries_the_hits_avatar_url()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, zoeId, zoeHandle) = await NewUserAsync();
        await SeedAvatarAsync(zoeId);

        var result = await ada.GetFromJsonAsync<JsonElement>($"/api/v1/chat/search?q={zoeHandle}", Json);
        var hit = result.GetProperty("people").GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("userId").GetGuid() == zoeId);

        Assert.Equal(ExpectedUrl(zoeHandle), hit.GetProperty("avatarUrl").GetString());
    }

    /// <summary>
    /// The member projection returns a placeholder name for a banned/erased account by design
    /// (ChatConversationService.cs:716-719); the avatar must not become a way around it. A banned
    /// member's profile is filtered out globally, so both name and avatar fall back together.
    /// </summary>
    [Fact]
    public async Task Banned_members_avatar_stays_hidden_alongside_the_placeholder_name()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        await SeedAvatarAsync(benId);

        var conversationId = await SeedConversationAsync(ConversationKind.Group, null, adaId, benId);
        await BanAsync(benId);

        var members = await ada.GetFromJsonAsync<JsonElement>(
            $"/api/v1/chat/conversations/{conversationId}/members", Json);
        var ben = members.GetProperty("items").EnumerateArray()
            .Single(m => m.GetProperty("userId").GetGuid() == benId);

        Assert.Null(ben.GetProperty("avatarUrl").GetString());
        // The name is the placeholder — proving name and avatar hide together (default culture → English).
        Assert.Equal(JuggerHub.Common.MemberPlaceholder.English, ben.GetProperty("displayName").GetString());
    }
}
