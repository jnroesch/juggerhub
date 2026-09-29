using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Parties;

/// <summary>
/// The party page's way into the party chat (feature 063, GH #382): <c>GET /chat/party/{partyId}</c>,
/// the sibling of feature 060's team-chat resolver.
/// </summary>
/// <remarks>
/// The crew is every <see cref="PartyMember"/> who is <see cref="PartyMemberStatus.In"/> — party admins,
/// team members and marketplace guests alike. Everyone else who can see the party page (team members who
/// have not answered or have declined) must get the same 404 as a stranger or a made-up id, and asking
/// must create nothing. These tests live beside the other party suites because the party seeding helpers
/// are in <see cref="PartyTestSupport"/>.
/// </remarks>
[Collection("Parties")]
public sealed class PartyChatLinkTests : PartyTestSupport
{
    public PartyChatLinkTests(JuggerHubApiFactory factory) : base(factory) { }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // --- helpers ---------------------------------------------------------------

    private static Task<HttpResponseMessage> OpenPartyChatAsync(HttpClient client, Guid partyId) =>
        client.GetAsync($"/api/v1/chat/party/{partyId}");

    private static async Task<Guid> OpenPartyChatIdAsync(HttpClient client, Guid partyId)
    {
        var resp = await OpenPartyChatAsync(client, partyId);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("conversationId").GetGuid();
    }

    /// <summary>The inbox row for a party's chat, by kind and party.</summary>
    private static async Task<Guid?> InboxPartyChatIdAsync(HttpClient client, Guid partyId)
    {
        var resp = await client.GetAsync("/api/v1/chat/conversations");
        resp.EnsureSuccessStatusCode();
        var inbox = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        foreach (var c in inbox.GetProperty("items").EnumerateArray())
        {
            if (c.GetProperty("kind").GetString() == "Party"
                && c.GetProperty("partyId").ValueKind != JsonValueKind.Null
                && c.GetProperty("partyId").GetGuid() == partyId)
            {
                return c.GetProperty("id").GetGuid();
            }
        }

        return null;
    }

    private async Task<int> PartyChatCountAsync(Guid partyId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Conversations.CountAsync(c => c.Kind == ConversationKind.Party && c.PartyId == partyId);
    }

    private async Task<int> NotificationCountAsync(params Guid[] recipients)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Notifications.CountAsync(n => recipients.Contains(n.RecipientUserId));
    }

    /// <summary>Seat a marketplace guest: in the crew, not on the team — the state 017's accept path leaves.</summary>
    private async Task SeatGuestAsync(Guid partyId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PartyMembers.Add(new PartyMember
        {
            PartyId = partyId,
            UserId = userId,
            Status = PartyMemberStatus.In,
            Role = PartyMemberRole.Member,
            ViaMarket = true,
        });
        await db.SaveChangesAsync();
    }

    private static async Task JoinAsync(HttpClient client, Guid partyId) =>
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/parties/{partyId}/join", null)).StatusCode);

    /// <summary>A team with an admin (who forms the party, so is its admin and in its crew) and one more member.</summary>
    private async Task<(HttpClient Admin, Guid AdminId, HttpClient Member, Guid MemberId, Guid TeamId, Guid PartyId)> PartyAsync()
    {
        var (admin, adminId, _, _) = await NewUserAsync();
        var (member, memberId, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        return (admin, adminId, member, memberId, teamId, partyId);
    }

    // --- who gets the chat -------------------------------------------------------

    /// <summary>FR-002: the button opens the conversation the inbox lists as the party's chat.</summary>
    [Fact]
    public async Task A_crew_member_gets_the_party_chat_the_inbox_lists()
    {
        var (_, _, member, _, _, partyId) = await PartyAsync();
        await JoinAsync(member, partyId);

        var fromInbox = await InboxPartyChatIdAsync(member, partyId);
        var fromButton = await OpenPartyChatIdAsync(member, partyId);

        Assert.NotNull(fromInbox);
        Assert.Equal(fromInbox, fromButton);
    }

    /// <summary>US2: the party's admin (its creator) gets the same chat.</summary>
    [Fact]
    public async Task A_party_admin_gets_the_party_chat()
    {
        var (admin, _, member, _, _, partyId) = await PartyAsync();
        await JoinAsync(member, partyId);

        var fromMember = await OpenPartyChatIdAsync(member, partyId);
        var fromAdmin = await OpenPartyChatIdAsync(admin, partyId);

        Assert.Equal(fromMember, fromAdmin);
    }

    /// <summary>US1-3: a marketplace guest is in the crew without being on the team, and gets the chat too.</summary>
    [Fact]
    public async Task A_marketplace_guest_gets_the_party_chat()
    {
        var (admin, _, _, _, _, partyId) = await PartyAsync();
        var (guest, guestId, _, _) = await NewUserAsync();
        await SeatGuestAsync(partyId, guestId);

        var fromGuest = await OpenPartyChatIdAsync(guest, partyId);

        Assert.Equal(fromGuest, await OpenPartyChatIdAsync(admin, partyId));
    }

    /// <summary>FR-003: nobody has opened Chat yet — pressing creates the chat and tells nobody anything.</summary>
    [Fact]
    public async Task A_never_opened_party_chat_is_created_and_nothing_is_sent()
    {
        var (_, adminId, member, memberId, _, partyId) = await PartyAsync();
        await JoinAsync(member, partyId);
        Assert.Equal(0, await PartyChatCountAsync(partyId));

        // Before/after counts, not "never": forming the party already told the team (email + push).
        var notificationsBefore = await NotificationCountAsync(adminId, memberId);
        var emailsBefore = Factory.EmailSender.Sent.Count;
        var pushesBefore = PushesTo(adminId, memberId);

        var id = await OpenPartyChatIdAsync(member, partyId);

        Assert.Equal(1, await PartyChatCountAsync(partyId));
        var messages = await member.GetFromJsonAsync<JsonElement>($"/api/v1/chat/conversations/{id}/messages", Json);
        Assert.Equal(0, messages.GetProperty("items").GetArrayLength());
        Assert.Equal(notificationsBefore, await NotificationCountAsync(adminId, memberId));
        Assert.Equal(emailsBefore, Factory.EmailSender.Sent.Count);
        Assert.Equal(pushesBefore, PushesTo(adminId, memberId));
    }

    private int PushesTo(params Guid[] users) =>
        Factory.PushDispatcher.Dispatches.Count(d => d.RecipientUserIds.Any(users.Contains));

    // --- who does not ------------------------------------------------------------

    /// <summary>
    /// FR-001/FR-007: a team member who has not answered sees the party page but is not in the crew —
    /// refused, and asking creates no chat for the party.
    /// </summary>
    [Fact]
    public async Task A_team_member_who_has_not_answered_gets_404_and_creates_no_chat()
    {
        var (_, _, member, _, _, partyId) = await PartyAsync();

        var resp = await OpenPartyChatAsync(member, partyId);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await PartyChatCountAsync(partyId));
    }

    /// <summary>FR-001: declining is not being in the crew.</summary>
    [Fact]
    public async Task A_team_member_who_declined_gets_404()
    {
        var (_, _, member, _, _, partyId) = await PartyAsync();
        Assert.Equal(HttpStatusCode.OK, (await member.PostAsync($"/api/v1/parties/{partyId}/decline", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await OpenPartyChatAsync(member, partyId)).StatusCode);
    }

    /// <summary>FR-011, the server's half: someone who left the crew after the page loaded is refused.</summary>
    [Fact]
    public async Task A_former_crew_member_gets_404()
    {
        var (_, _, member, _, _, partyId) = await PartyAsync();
        await JoinAsync(member, partyId);
        await OpenPartyChatIdAsync(member, partyId);

        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"/api/v1/parties/{partyId}/leave", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await OpenPartyChatAsync(member, partyId)).StatusCode);
    }

    /// <summary>
    /// Spec Context: disbanding removes the party and archives its chat, which clears the chat's link to
    /// the party — so the lookup finds nothing and the answer is the ordinary 404.
    /// </summary>
    [Fact]
    public async Task A_disbanded_party_gets_404()
    {
        var (admin, _, member, _, _, partyId) = await PartyAsync();
        await JoinAsync(member, partyId);
        await OpenPartyChatIdAsync(member, partyId);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/parties/{partyId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await OpenPartyChatAsync(member, partyId)).StatusCode);
    }

    /// <summary>
    /// Research R2: another kind's id is not a party. The team's chat exists and the caller is on the team,
    /// but asking for it as a party must not find it.
    /// </summary>
    [Fact]
    public async Task A_team_id_is_not_a_party_id()
    {
        var (admin, _, _, _, teamId, _) = await PartyAsync();
        (await admin.GetAsync("/api/v1/chat/conversations")).EnsureSuccessStatusCode(); // the team chat now exists

        Assert.Equal(HttpStatusCode.NotFound, (await OpenPartyChatAsync(admin, teamId)).StatusCode);
    }

    /// <summary>
    /// <b>FR-005 / SC-003.</b> Not in the crew, and no such party, read the same — so the endpoint cannot
    /// be used to learn whether a party exists or has a chat.
    /// </summary>
    [Fact]
    public async Task A_refusal_reads_the_same_whether_or_not_the_party_exists()
    {
        var (admin, _, _, _, _, partyId) = await PartyAsync();
        var (stranger, _, _, _) = await NewUserAsync();
        await OpenPartyChatIdAsync(admin, partyId); // the party's chat exists

        var notCrew = await OpenPartyChatAsync(stranger, partyId);
        var noSuchParty = await OpenPartyChatAsync(stranger, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, notCrew.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noSuchParty.StatusCode);
        var a = await notCrew.Content.ReadFromJsonAsync<JsonElement>(Json);
        var b = await noSuchParty.Content.ReadFromJsonAsync<JsonElement>(Json);
        foreach (var field in new[] { "title", "detail", "status" })
        {
            Assert.Equal(a.GetProperty(field).ToString(), b.GetProperty(field).ToString());
        }
    }

    // --- what the answer is and is not -----------------------------------------------

    /// <summary>FR-012: hidden and muted, it still opens — and pressing changes neither flag.</summary>
    [Fact]
    public async Task A_hidden_and_muted_party_chat_still_opens_and_stays_so()
    {
        var (_, _, member, memberId, _, partyId) = await PartyAsync();
        await JoinAsync(member, partyId);
        var id = await OpenPartyChatIdAsync(member, partyId);

        var patch = await member.PatchAsJsonAsync($"/api/v1/chat/conversations/{id}/state", new { isMuted = true, isHidden = true });
        Assert.Equal(HttpStatusCode.NoContent, patch.StatusCode);

        Assert.Equal(id, await OpenPartyChatIdAsync(member, partyId));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.ConversationParticipants.AsNoTracking().SingleAsync(p => p.ConversationId == id && p.UserId == memberId);
        Assert.True(state.IsHidden);
        Assert.True(state.IsMuted);
    }

    /// <summary>FR-006: the answer is the id and nothing else.</summary>
    [Fact]
    public async Task The_answer_carries_only_the_conversation_id()
    {
        var (admin, _, _, _, _, partyId) = await PartyAsync();

        var resp = await OpenPartyChatAsync(admin, partyId);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);

        var property = Assert.Single(body.EnumerateObject());
        Assert.Equal("conversationId", property.Name);
    }

    /// <summary>FR-013: crew members pressing at once on a never-opened chat land in the same, single chat.</summary>
    [Fact]
    public async Task Concurrent_first_presses_leave_one_party_chat()
    {
        var (admin, _, member, _, _, partyId) = await PartyAsync();
        await JoinAsync(member, partyId);

        var ids = await Task.WhenAll(
            OpenPartyChatIdAsync(admin, partyId),
            OpenPartyChatIdAsync(member, partyId),
            OpenPartyChatIdAsync(admin, partyId));

        Assert.Single(ids.Distinct());
        Assert.Equal(1, await PartyChatCountAsync(partyId));
    }

    [Fact]
    public async Task Without_a_session_the_answer_is_401()
    {
        var (_, _, _, _, _, partyId) = await PartyAsync();

        var resp = await OpenPartyChatAsync(Factory.CreateClient(), partyId);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
