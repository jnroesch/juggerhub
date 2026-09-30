using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Parties;

/// <summary>
/// What a disband does to the party's chat (feature 019, data-model R3a; GH #400).
/// </summary>
/// <remarks>
/// A disband is a hard delete, so the chat is archived first: the crew is frozen into participant
/// rows, which from then on are the membership. The crew at that moment keeps the history. Someone who
/// was in the crew earlier, opened the chat and has since left or been removed must not — their
/// leftover state row used to read as membership once the chat was archived. A disband is routine, so
/// this is the path on which that happened in normal use.
/// </remarks>
[Collection("Parties")]
public sealed class PartyChatArchiveTests : PartyTestSupport
{
    public PartyChatArchiveTests(JuggerHubApiFactory factory) : base(factory) { }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // --- helpers ---------------------------------------------------------------

    private static async Task<Guid> OpenPartyChatIdAsync(HttpClient client, Guid partyId)
    {
        var resp = await client.GetAsync($"/api/v1/chat/party/{partyId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("conversationId").GetGuid();
    }

    private static async Task<Guid> SendAsync(HttpClient client, Guid conversationId, string body)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages", new { body });
        Assert.True(resp.IsSuccessStatusCode, $"send failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return (await resp.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private static async Task MarkReadAsync(HttpClient client, Guid conversationId, Guid messageId) =>
        (await client.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/read",
            new { lastReadMessageId = messageId })).EnsureSuccessStatusCode();

    private static async Task<List<string?>> BodiesAsync(HttpClient client, Guid conversationId)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/chat/conversations/{conversationId}/messages", Json);
        return page.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("body").GetString()).ToList();
    }

    private static async Task<bool> InInboxAsync(HttpClient client, Guid conversationId)
    {
        var inbox = await client.GetFromJsonAsync<JsonElement>("/api/v1/chat/conversations", Json);
        return inbox.GetProperty("items").EnumerateArray().Any(c => c.GetProperty("id").GetGuid() == conversationId);
    }

    /// <summary>Not in the inbox, and 404 for the conversation and for its history.</summary>
    private static async Task AssertShutOutAsync(HttpClient client, Guid conversationId)
    {
        Assert.False(await InInboxAsync(client, conversationId));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/chat/conversations/{conversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/chat/conversations/{conversationId}/messages")).StatusCode);
    }

    /// <summary>A party whose admin formed it, with one more team member who has joined the crew.</summary>
    private async Task<(HttpClient Admin, HttpClient Member, Guid MemberId, Guid PartyId)> PartyWithCrewAsync()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (member, memberId, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        Assert.Equal(HttpStatusCode.OK, (await member.PostAsync($"/api/v1/parties/{partyId}/join", null)).StatusCode);
        return (admin, member, memberId, partyId);
    }

    // --- the crew keeps it -------------------------------------------------------

    /// <summary>R3a for parties: the party is gone, the people who were in its crew still read.</summary>
    [Fact]
    public async Task The_crew_keeps_the_party_chat_after_a_disband()
    {
        var (admin, member, _, partyId) = await PartyWithCrewAsync();
        var conversationId = await OpenPartyChatIdAsync(admin, partyId);
        await SendAsync(admin, conversationId, "meet at the north gate");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/parties/{partyId}")).StatusCode);

        // The member never opened the chat, so had no state row: the snapshot is what lets them in.
        foreach (var reader in new[] { admin, member })
        {
            Assert.True(await InInboxAsync(reader, conversationId));
            Assert.Contains("meet at the north gate", await BodiesAsync(reader, conversationId));
        }

        // Closed to writes.
        var send = await member.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages", new { body = "hello?" });
        Assert.Equal(HttpStatusCode.Conflict, send.StatusCode);
    }

    // --- whoever left it does not -------------------------------------------------

    /// <summary><b>GH #400.</b> Removed from the crew after reading the chat, then the party disbands.</summary>
    [Fact]
    public async Task A_removed_crew_member_does_not_get_the_party_chat_back_on_disband()
    {
        var (admin, member, memberId, partyId) = await PartyWithCrewAsync();

        // The member opens the chat and reads it, which is what gives them a state row.
        var conversationId = await OpenPartyChatIdAsync(member, partyId);
        var seen = await SendAsync(admin, conversationId, "meet at the north gate");
        await MarkReadAsync(member, conversationId, seen);

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"/api/v1/parties/{partyId}/members/{memberId}")).StatusCode);
        await AssertShutOutAsync(member, conversationId);

        await SendAsync(admin, conversationId, "written after the removal");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/parties/{partyId}")).StatusCode);

        // Archiving must not reopen it.
        await AssertShutOutAsync(member, conversationId);
        Assert.Contains("written after the removal", await BodiesAsync(admin, conversationId));
    }

    /// <summary>The same for someone who left the crew themselves.</summary>
    [Fact]
    public async Task A_crew_member_who_left_does_not_get_the_party_chat_back_on_disband()
    {
        var (admin, member, _, partyId) = await PartyWithCrewAsync();

        var conversationId = await OpenPartyChatIdAsync(member, partyId);
        var seen = await SendAsync(admin, conversationId, "meet at the north gate");
        await MarkReadAsync(member, conversationId, seen);

        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"/api/v1/parties/{partyId}/leave", null)).StatusCode);
        await SendAsync(admin, conversationId, "written after they left");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/parties/{partyId}")).StatusCode);

        await AssertShutOutAsync(member, conversationId);
    }
}
