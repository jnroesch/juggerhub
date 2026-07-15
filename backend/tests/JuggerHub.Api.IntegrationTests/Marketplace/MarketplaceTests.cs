using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Parties;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Marketplace;

/// <summary>
/// Event marketplace (017) end-to-end: post listing → recruit → apply/invite handshake → accept →
/// guest membership → cap → reach (inbox/dashboard/notification) → direct invite → cleanup, plus the
/// security boundaries. Reuses the shared Parties Testcontainers Postgres host + party seed helpers.
/// </summary>
[Collection("Parties")]
public sealed class MarketplaceTests : PartyTestSupport
{
    public MarketplaceTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- US1: post yourself as a mercenary ------------------------------------

    [Fact]
    public async Task Eligible_user_posts_a_listing_that_shows_on_the_board()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var (merc, _, mercHandle, _) = await NewUserAsync();

        var post = await merc.PostAsJsonAsync($"/api/v1/events/{eventId}/market/listing",
            new { positions = new[] { "Laeufer", "QTip" }, pitch = "Quick feet, will travel." });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var board = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/market/free-agents");
        Assert.Equal(1, board.GetProperty("totalCount").GetInt32());
        var card = board.GetProperty("items")[0];
        Assert.Equal(mercHandle, card.GetProperty("handle").GetString());
        Assert.Contains("Laeufer", card.GetProperty("positions").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Board_is_public_and_filters_by_position()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var (merc, _, _, _) = await NewUserAsync();
        await PostListingAsync(merc, eventId, "Schild");

        var anon = Factory.CreateClient();
        var schild = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/market/free-agents?position=Schild");
        Assert.Equal(1, schild.GetProperty("totalCount").GetInt32());
        var kette = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/market/free-agents?position=Kette");
        Assert.Equal(0, kette.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Member_of_a_party_cannot_post_a_listing()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        await FormPartyAsync(admin, eventId, teamId); // admin is In their own party

        var resp = await admin.PostAsJsonAsync($"/api/v1/events/{eventId}/market/listing",
            new { positions = new[] { "Laeufer" }, pitch = "Me too." });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task One_listing_per_user_then_edit_and_take_down()
    {
        var (host, _, _, _) = await NewUserAsync();
        var eventId = await CreateTeamsEventAsync(host);
        var (merc, _, _, _) = await NewUserAsync();
        await PostListingAsync(merc, eventId, "Laeufer");

        var again = await merc.PostAsJsonAsync($"/api/v1/events/{eventId}/market/listing",
            new { positions = new[] { "Kette" }, pitch = "Second." });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var edit = await merc.PutAsJsonAsync($"/api/v1/events/{eventId}/market/listing",
            new { positions = new[] { "Kette" }, pitch = "Edited pitch." });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var down = await merc.DeleteAsync($"/api/v1/events/{eventId}/market/listing");
        Assert.Equal(HttpStatusCode.NoContent, down.StatusCode);

        var board = await host.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/market/free-agents");
        Assert.Equal(0, board.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Listing_not_offered_on_an_individuals_event()
    {
        var (host, _, _, _) = await NewUserAsync();
        var eventId = await CreateIndividualsEventAsync(host);
        var (merc, _, _, _) = await NewUserAsync();

        var me = await merc.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/market/me");
        Assert.Equal("Individuals", me.GetProperty("mode").GetString());
        Assert.False(me.GetProperty("eligible").GetBoolean());

        var post = await merc.PostAsJsonAsync($"/api/v1/events/{eventId}/market/listing",
            new { positions = new[] { "Laeufer" }, pitch = "No." });
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    // --- US2: recruiting ------------------------------------------------------

    [Fact]
    public async Task Recruiting_defaults_off_and_admin_can_list_the_party()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var before = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/recruiting");
        Assert.False(before.GetProperty("isRecruiting").GetBoolean());

        var board0 = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/market/parties");
        Assert.Equal(0, board0.GetProperty("totalCount").GetInt32());

        await SetRecruitingAsync(admin, partyId, true, position: "Laeufer", spots: 2);

        var board1 = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/market/parties");
        Assert.Equal(1, board1.GetProperty("totalCount").GetInt32());
        Assert.Equal(7, board1.GetProperty("items")[0].GetProperty("openSpots").GetInt32()); // cap 8 − 1 in
    }

    [Fact]
    public async Task Non_admin_cannot_toggle_recruiting()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (member, memberId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var resp = await member.PutAsJsonAsync($"/api/v1/parties/{partyId}/recruiting",
            new { isRecruiting = true, spotsAdvertised = 2, positionsNeeded = new[] { "Laeufer" }, blurb = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Turning_recruiting_off_keeps_received_applications()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await SetRecruitingAsync(admin, partyId, true);
        var (merc, _, _, _) = await NewUserAsync();
        await ApplyAsync(merc, partyId);

        await SetRecruitingAsync(admin, partyId, false);

        var apps = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/market/applications");
        Assert.Equal(1, apps.GetProperty("totalCount").GetInt32());
    }

    // --- US3: the handshake ---------------------------------------------------

    [Fact]
    public async Task Apply_and_invite_create_pending_requests_visible_to_both_sides()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await SetRecruitingAsync(admin, partyId, true);

        var (applicant, _, _, _) = await NewUserAsync();
        await ApplyAsync(applicant, partyId);
        var apps = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/market/applications");
        Assert.Equal(1, apps.GetProperty("totalCount").GetInt32());
        Assert.Equal("Application", apps.GetProperty("items")[0].GetProperty("direction").GetString());

        var (invitee, inviteeId, _, inviteeEmail) = await NewUserAsync();
        Factory.EmailSender.Clear();
        await InviteAsync(admin, partyId, inviteeId);
        Assert.NotNull(Factory.EmailSender.LatestFor(inviteeEmail));

        var sent = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/market/invites");
        Assert.Equal(1, sent.GetProperty("totalCount").GetInt32());

        var mine = await invitee.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/market/me");
        Assert.Equal(1, mine.GetProperty("invitesToAnswer").GetArrayLength());
    }

    [Fact]
    public async Task Duplicate_application_is_refused()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await SetRecruitingAsync(admin, partyId, true);
        var (merc, _, _, _) = await NewUserAsync();
        await ApplyAsync(merc, partyId);

        var again = await merc.PostAsJsonAsync($"/api/v1/parties/{partyId}/market/applications", new { positions = new[] { "Laeufer" } });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Revoke_and_decline_drop_the_request_without_seating()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await SetRecruitingAsync(admin, partyId, true);

        // Applicant withdraws their own application.
        var (applicant, _, _, _) = await NewUserAsync();
        var appId = await ApplyAsync(applicant, partyId);
        var revoke = await applicant.PostAsync($"/api/v1/market/requests/{appId}/revoke", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        // Invitee declines an invite.
        var (invitee, inviteeId, _, _) = await NewUserAsync();
        var invId = await InviteAsync(admin, partyId, inviteeId);
        var decline = await invitee.PostAsync($"/api/v1/market/requests/{invId}/decline", null);
        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);

        var party = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}");
        Assert.Equal(1, party.GetProperty("inCount").GetInt32()); // only the admin
    }

    [Fact]
    public async Task Wrong_actor_cannot_answer_or_withdraw()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await SetRecruitingAsync(admin, partyId, true);
        var (applicant, _, _, _) = await NewUserAsync();
        var appId = await ApplyAsync(applicant, partyId);
        var (outsider, _, _, _) = await NewUserAsync();

        // The applicant is not the recipient — cannot accept their own application.
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.PostAsync($"/api/v1/market/requests/{appId}/accept", null)).StatusCode);
        // An outsider can neither accept nor revoke.
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsync($"/api/v1/market/requests/{appId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsync($"/api/v1/market/requests/{appId}/revoke", null)).StatusCode);
    }

    // --- US4: accept lands the guest ------------------------------------------

    [Fact]
    public async Task Accepting_an_application_seats_a_guest_and_cleans_up()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await SetRecruitingAsync(admin, partyId, true);

        var (merc, mercId, _, _) = await NewUserAsync();
        await PostListingAsync(merc, eventId, "Laeufer");       // has a listing
        var otherPartyId = await FormSecondRecruitingPartyAsync(eventId); // another crew
        await ApplyAsync(merc, otherPartyId);                    // and a second pending application
        var appId = await ApplyAsync(merc, partyId);

        var accept = await admin.PostAsync($"/api/v1/market/requests/{appId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // Guest is In, tagged ViaMarket, counted.
        var party = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}");
        Assert.Equal(2, party.GetProperty("inCount").GetInt32());
        var inGroup = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/members?group=in");
        var guest = inGroup.GetProperty("items").EnumerateArray().Single(m => m.GetProperty("userId").GetGuid() == mercId);
        Assert.True(guest.GetProperty("viaMarket").GetBoolean());

        // Listing gone + other pending application revoked.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.MercenaryListings.AnyAsync(l => l.EventId == eventId && l.UserId == mercId));
        Assert.False(await db.MarketRequests.AnyAsync(r => r.PartyId == otherPartyId && r.UserId == mercId && r.Status == MarketRequestStatus.Pending));
        // No separate individual signup for the guest.
        Assert.False(await db.EventSignups.AnyAsync(s => s.EventId == eventId && s.UserId == mercId));
    }

    [Fact]
    public async Task Invited_player_accepts_from_the_other_direction()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var (invitee, inviteeId, _, _) = await NewUserAsync();
        var invId = await InviteAsync(admin, partyId, inviteeId);
        var accept = await invitee.PostAsync($"/api/v1/market/requests/{invId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var party = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}");
        Assert.Equal(2, party.GetProperty("inCount").GetInt32());
    }

    [Fact]
    public async Task Cap_is_enforced_atomically_under_concurrent_accepts()
    {
        // A 1-slot party (cap 5, four team members already In) with two applicants racing for the seat.
        var (admin, adminId, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin, rosterCap: 5);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await SetRecruitingAsync(admin, partyId, true);
        // admin is In (1); add 3 team members → 4 In of cap 5, leaving exactly one open seat.
        await SeedInMembersAsync(partyId, teamId, 3);

        var (a1, _, _, _) = await NewUserAsync();
        var (a2, _, _, _) = await NewUserAsync();
        var app1 = await ApplyAsync(a1, partyId);
        var app2 = await ApplyAsync(a2, partyId);

        var t1 = admin.PostAsync($"/api/v1/market/requests/{app1}/accept", null);
        var t2 = admin.PostAsync($"/api/v1/market/requests/{app2}/accept", null);
        var results = await Task.WhenAll(t1, t2);

        var ok = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var full = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, ok);
        Assert.Equal(1, full);

        var party = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}");
        Assert.Equal(5, party.GetProperty("inCount").GetInt32()); // never exceeds cap
    }

    // --- US6: direct invite ---------------------------------------------------

    [Fact]
    public async Task Direct_search_annotates_relations_and_blocks_ineligible()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var (_, _, freeHandle, _) = await NewUserAsync(); // an eligible free agent

        // Seat someone into another crew so they're ineligible to be invited here.
        var (admin2, _, _, _) = await NewUserAsync();
        var (teamId2, _) = await CreateTeamAsync(admin2);
        var otherPartyId = await FormPartyAsync(admin2, eventId, teamId2);
        var (seated, seatedId, _, _) = await NewUserAsync();
        var seatInv = await InviteAsync(admin2, otherPartyId, seatedId);
        (await seated.PostAsync($"/api/v1/market/requests/{seatInv}/accept", null)).EnsureSuccessStatusCode();

        var search = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/market/user-search?query={freeHandle}");
        Assert.Equal("Invitable", search.GetProperty("items")[0].GetProperty("relation").GetString());

        var block = await admin.PostAsJsonAsync($"/api/v1/parties/{partyId}/market/invites",
            new { userId = seatedId, positions = new[] { "Laeufer" } });
        Assert.Equal(HttpStatusCode.Conflict, block.StatusCode);
    }

    // --- US7: guest lifecycle + cleanup ---------------------------------------

    [Fact]
    public async Task Guest_can_read_the_party_and_removal_frees_the_spot()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        var (guest, guestId, _, _) = await NewUserAsync();
        var invId = await InviteAsync(admin, partyId, guestId);
        await guest.PostAsync($"/api/v1/market/requests/{invId}/accept", null);

        // Guest (crew, not team member) can read the party hub.
        var seen = await guest.GetAsync($"/api/v1/parties/{partyId}");
        Assert.Equal(HttpStatusCode.OK, seen.StatusCode);

        // Admin removes the guest → spot frees, no team membership created.
        var remove = await admin.DeleteAsync($"/api/v1/parties/{partyId}/members/{guestId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.TeamMemberships.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == guestId));
        Assert.False(await db.PartyMembers.AnyAsync(m => m.PartyId == partyId && m.UserId == guestId));
    }

    [Fact]
    public async Task Disband_cascades_market_requests()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await SetRecruitingAsync(admin, partyId, true);
        var (merc, _, _, _) = await NewUserAsync();
        await ApplyAsync(merc, partyId);

        var disband = await admin.DeleteAsync($"/api/v1/parties/{partyId}");
        Assert.Equal(HttpStatusCode.NoContent, disband.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.MarketRequests.AnyAsync(r => r.PartyId == partyId));
    }

    [Fact]
    public async Task Dashboard_returns_only_the_callers_pending_items()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await SetRecruitingAsync(admin, partyId, true);
        var (merc, _, _, _) = await NewUserAsync();
        await ApplyAsync(merc, partyId);

        var mine = await merc.GetFromJsonAsync<JsonElement>("/api/v1/market/mine");
        Assert.Equal(1, mine.GetProperty("totalCount").GetInt32());

        var adminMine = await admin.GetFromJsonAsync<JsonElement>("/api/v1/market/mine");
        Assert.Equal(0, adminMine.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Dashboard_lists_the_callers_active_listings_across_events()
    {
        var (host, _, _, _) = await NewUserAsync();
        var e1 = await CreateTeamsEventAsync(host);
        var e2 = await CreateTeamsEventAsync(host);
        var (merc, _, _, _) = await NewUserAsync();
        await PostListingAsync(merc, e1, "Laeufer");
        await PostListingAsync(merc, e2, "Schild");

        var mine = await merc.GetFromJsonAsync<JsonElement>("/api/v1/market/mine/listings");
        Assert.Equal(2, mine.GetProperty("totalCount").GetInt32());
        var names = mine.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("eventName").GetString()).ToList();
        Assert.All(names, n => Assert.False(string.IsNullOrEmpty(n)));

        // Scoped to the caller — another player sees none of these.
        var (other, _, _, _) = await NewUserAsync();
        var none = await other.GetFromJsonAsync<JsonElement>("/api/v1/market/mine/listings");
        Assert.Equal(0, none.GetProperty("totalCount").GetInt32());
    }

    // --- Local marketplace helpers --------------------------------------------

    private async Task<Guid> CreateIndividualsEventAsync(HttpClient client)
    {
        object body = new
        {
            name = "Solo Open", type = "Tournament", description = "Individuals.",
            startsAt = "2026-09-05T09:00:00Z", endsAt = "2026-09-06T18:00:00Z",
            locationKind = "Virtual", virtualLink = "https://jugger.example/solo",
            participantMode = "Individuals", participationLimit = 16, rosterCap = (int?)null,
            isPaid = false,
        };
        var resp = await client.PostAsJsonAsync("/api/v1/events", body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task PostListingAsync(HttpClient client, Guid eventId, string position)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/events/{eventId}/market/listing",
            new { positions = new[] { position }, pitch = "Ready to play." });
        resp.EnsureSuccessStatusCode();
    }

    private async Task SetRecruitingAsync(HttpClient admin, Guid partyId, bool on, string position = "Laeufer", int spots = 2)
    {
        var resp = await admin.PutAsJsonAsync($"/api/v1/parties/{partyId}/recruiting",
            new { isRecruiting = on, spotsAdvertised = spots, positionsNeeded = new[] { position }, blurb = "Come play." });
        resp.EnsureSuccessStatusCode();
    }

    private async Task<Guid> ApplyAsync(HttpClient client, Guid partyId)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/parties/{partyId}/market/applications", new { positions = new[] { "Laeufer" } });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> InviteAsync(HttpClient admin, Guid partyId, Guid userId)
    {
        var resp = await admin.PostAsJsonAsync($"/api/v1/parties/{partyId}/market/invites", new { userId, positions = new[] { "Laeufer" } });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Create a second team+admin+recruiting party for the same event; returns its party id.</summary>
    private async Task<Guid> FormSecondRecruitingPartyAsync(Guid eventId)
    {
        var (admin2, _, _, _) = await NewUserAsync();
        var (teamId2, _) = await CreateTeamAsync(admin2);
        var partyId2 = await FormPartyAsync(admin2, eventId, teamId2);
        await SetRecruitingAsync(admin2, partyId2, true);
        return partyId2;
    }

    /// <summary>Seed N extra team members and put them In the party (bypassing the request dance).</summary>
    private async Task SeedInMembersAsync(Guid partyId, Guid teamId, int count)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var i = 0; i < count; i++)
        {
            var (_, uid, _, _) = await NewUserAsync();
            db.TeamMemberships.Add(new TeamMembership { TeamId = teamId, UserId = uid, Role = TeamRole.Member, JoinedDate = DateTime.UtcNow });
            db.PartyMembers.Add(new PartyMember { PartyId = partyId, UserId = uid, Status = PartyMemberStatus.In, Role = PartyMemberRole.Member });
        }

        await db.SaveChangesAsync();
    }
}
