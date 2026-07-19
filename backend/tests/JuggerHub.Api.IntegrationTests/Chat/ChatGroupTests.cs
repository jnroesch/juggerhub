using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Named groups (feature 019, User Story 3).
/// </summary>
[Collection("Chat")]
public sealed class ChatGroupTests : ChatTestSupport
{
    public ChatGroupTests(JuggerHubApiFactory factory) : base(factory) { }

    private static async Task<Guid> StartGroupAsync(HttpClient client, string name, params Guid[] others)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = others, name });
        Assert.True(resp.IsSuccessStatusCode, $"start group failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Everyone_picked_lands_in_the_group()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (nia, niaId, _) = await NewUserAsync();

        var groupId = await StartGroupAsync(ada, "Weekend crew", benId, niaId);
        await SendAsync(ada, groupId, "carpool at 8?");

        foreach (var member in new[] { ben, nia })
        {
            var page = await GetMessagesAsync(member, groupId);
            Assert.Equal("carpool at 8?", page.GetProperty("items")[0].GetProperty("body").GetString());
        }
    }

    /// <summary>FR-012: a group labels other people's messages; your own are not labelled.</summary>
    [Fact]
    public async Task A_group_labels_other_peoples_messages_with_the_sender()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();

        var groupId = await StartGroupAsync(ada, "Weekend crew", benId, niaId);
        await SendAsync(ada, groupId, "from ada");

        var forBen = await GetMessagesAsync(ben, groupId);
        var m = forBen.GetProperty("items")[0];
        Assert.False(m.GetProperty("isOwn").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("senderName").GetString()));

        var forAda = await GetMessagesAsync(ada, groupId);
        Assert.True(forAda.GetProperty("items")[0].GetProperty("isOwn").GetBoolean());
        Assert.Equal(JsonValueKind.Null, forAda.GetProperty("items")[0].GetProperty("senderName").ValueKind);
    }

    [Fact]
    public async Task Any_member_can_add_people_and_a_system_line_records_it()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();
        var (kofi, kofiId, _) = await NewUserAsync();

        var groupId = await StartGroupAsync(ada, "Weekend crew", benId, niaId);

        // Ben is a plain member — groups have no admin role (spec Assumptions).
        var add = await ben.PostAsJsonAsync($"/api/v1/chat/conversations/{groupId}/members",
            new { userIds = new[] { kofiId } });
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);

        // Kofi sees the group.
        var inbox = await GetInboxAsync(kofi);
        Assert.Contains(inbox.GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == groupId);

        // A quiet system line records the join, attributable to nobody.
        var page = await GetMessagesAsync(ada, groupId);
        var system = page.GetProperty("items").EnumerateArray()
            .Where(m => m.GetProperty("kind").GetString() == "System")
            .ToList();
        Assert.Contains(system, m => m.GetProperty("systemEvent").GetString() == "Joined");
        Assert.All(system, m => Assert.Equal(JsonValueKind.Null, m.GetProperty("senderId").ValueKind));
    }

    /// <summary>Edge case: adding someone already in is a no-op — no duplicate member, no second system line.</summary>
    [Fact]
    public async Task Adding_the_same_person_twice_is_a_no_op()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();
        var (_, kofiId, _) = await NewUserAsync();

        var groupId = await StartGroupAsync(ada, "Weekend crew", benId, niaId);

        await ada.PostAsJsonAsync($"/api/v1/chat/conversations/{groupId}/members", new { userIds = new[] { kofiId } });
        await ada.PostAsJsonAsync($"/api/v1/chat/conversations/{groupId}/members", new { userIds = new[] { kofiId } });

        var members = await ada.GetFromJsonAsync<JsonElement>($"/api/v1/chat/conversations/{groupId}/members", Json);
        Assert.Equal(4, members.GetProperty("totalCount").GetInt32());

        var page = await GetMessagesAsync(ada, groupId);
        var joins = page.GetProperty("items").EnumerateArray()
            .Count(m => m.GetProperty("systemEvent").ValueKind != JsonValueKind.Null
                && m.GetProperty("systemEvent").GetString() == "Joined");
        Assert.Equal(1, joins);
    }

    /// <summary>US3 #6/#7: leaving keeps the group alive for everyone else — even when the creator leaves.</summary>
    [Fact]
    public async Task The_creator_leaving_does_not_strand_the_group()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (nia, niaId, _) = await NewUserAsync();

        var groupId = await StartGroupAsync(ada, "Weekend crew", benId, niaId);
        await SendAsync(ada, groupId, "before I go");

        var leave = await ada.DeleteAsync($"/api/v1/chat/conversations/{groupId}/members/me");
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);

        // Ada is out.
        Assert.Equal(HttpStatusCode.NotFound, (await ada.GetAsync($"/api/v1/chat/conversations/{groupId}")).StatusCode);

        // The others carry on, and can still send.
        await SendAsync(ben, groupId, "still here");
        var forNia = await GetMessagesAsync(nia, groupId);
        Assert.Contains(forNia.GetProperty("items").EnumerateArray(), m => m.GetProperty("body").GetString() == "still here");

        // A system line records the departure.
        Assert.Contains(forNia.GetProperty("items").EnumerateArray(),
            m => m.GetProperty("systemEvent").ValueKind != JsonValueKind.Null
                && m.GetProperty("systemEvent").GetString() == "Left");
    }

    /// <summary>US3 #8: a group down to one member still works — it is not silently deleted.</summary>
    [Fact]
    public async Task A_group_with_one_member_left_still_works()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (nia, niaId, _) = await NewUserAsync();

        var groupId = await StartGroupAsync(ada, "Weekend crew", benId, niaId);
        await ben.DeleteAsync($"/api/v1/chat/conversations/{groupId}/members/me");
        await nia.DeleteAsync($"/api/v1/chat/conversations/{groupId}/members/me");

        await SendAsync(ada, groupId, "anyone?");
        var page = await GetMessagesAsync(ada, groupId);
        Assert.Contains(page.GetProperty("items").EnumerateArray(), m => m.GetProperty("body").GetString() == "anyone?");
    }

    [Fact]
    public async Task The_group_cap_is_enforced()
    {
        var (ada, _, _) = await NewUserAsync();
        var many = Enumerable.Range(0, 50).Select(_ => Guid.CreateVersion7()).ToArray();

        var resp = await ada.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = many, name = "Too many" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    /// <summary>US3 #9 / FR-026: only a manual group can be added to.</summary>
    [Fact]
    public async Task Adding_to_a_direct_conversation_is_refused()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();

        var dmId = await StartDirectAsync(ada, benId);

        var resp = await ada.PostAsJsonAsync($"/api/v1/chat/conversations/{dmId}/members",
            new { userIds = new[] { niaId } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Leaving_a_direct_conversation_is_refused()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var dmId = await StartDirectAsync(ada, benId);

        var resp = await ada.DeleteAsync($"/api/v1/chat/conversations/{dmId}/members/me");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task A_non_member_cannot_add_to_a_group()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();
        var (mallory, _, _) = await NewUserAsync();

        var groupId = await StartGroupAsync(ada, "Weekend crew", benId, niaId);

        var resp = await mallory.PostAsJsonAsync($"/api/v1/chat/conversations/{groupId}/members",
            new { userIds = new[] { niaId } });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task The_details_panel_offers_leave_and_add_for_a_group_only()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, niaId, _) = await NewUserAsync();

        var groupId = await StartGroupAsync(ada, "Weekend crew", benId, niaId);
        var dmId = await StartDirectAsync(ada, benId);

        var group = await ada.GetFromJsonAsync<JsonElement>($"/api/v1/chat/conversations/{groupId}", Json);
        Assert.True(group.GetProperty("canLeave").GetBoolean());
        Assert.True(group.GetProperty("canAddMembers").GetBoolean());

        var dm = await ada.GetFromJsonAsync<JsonElement>($"/api/v1/chat/conversations/{dmId}", Json);
        Assert.False(dm.GetProperty("canLeave").GetBoolean());
        Assert.False(dm.GetProperty("canAddMembers").GetBoolean());
    }
}
