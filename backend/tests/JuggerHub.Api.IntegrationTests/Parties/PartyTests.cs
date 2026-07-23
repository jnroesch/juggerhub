using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Parties;

/// <summary>
/// Event parties (016) end-to-end: form → request → answer → manage → apply → cap → news →
/// co-admins → disband, plus the security boundaries. Exercises the real API + Postgres container.
/// </summary>
[Collection("Parties")]
public sealed class PartyTests : PartyTestSupport
{
    public PartyTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- US1: form ------------------------------------------------------------

    [Fact]
    public async Task Form_makes_creator_in_and_admin_with_cap_and_no_event_entry()
    {
        var (admin, adminId, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (org, _, _, _) = await NewUserAsync();
        var eventId = await CreateTeamsEventAsync(org, rosterCap: 8);

        var resp = await admin.PostAsJsonAsync("/api/v1/parties", new { eventId, teamId, message = "Who's in?" });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(8, dto.GetProperty("rosterCap").GetInt32());
        Assert.Equal(1, dto.GetProperty("inCount").GetInt32());
        Assert.Equal("Open", dto.GetProperty("status").GetString());
        Assert.Equal("Admin", dto.GetProperty("myState").GetString());
        Assert.True(dto.GetProperty("appliedGroup").ValueKind == JsonValueKind.Null);

        // No EventSignup was created for the team.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.EventSignups.AnyAsync(s => s.EventId == eventId && s.TeamId == teamId));
    }

    [Fact]
    public async Task Non_team_admin_cannot_form_a_party()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (member, memberId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);

        var resp = await member.PostAsJsonAsync("/api/v1/parties", new { eventId, teamId, message = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Only_one_party_per_team_and_event()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        await FormPartyAsync(admin, eventId, teamId);

        var second = await admin.PostAsJsonAsync("/api/v1/parties", new { eventId, teamId, message = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Party_context_lists_admin_team_and_reflects_existing_party()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);

        var before = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/party-context");
        Assert.Equal("Teams", before.GetProperty("mode").GetString());
        var team0 = before.GetProperty("teams")[0];
        Assert.True(team0.GetProperty("canForm").GetBoolean());
        Assert.Equal("None", team0.GetProperty("myState").GetString());

        await FormPartyAsync(admin, eventId, teamId);

        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/party-context");
        var team1 = after.GetProperty("teams")[0];
        Assert.False(team1.GetProperty("canForm").GetBoolean());
        Assert.Equal("Admin", team1.GetProperty("myState").GetString());
        Assert.False(string.IsNullOrEmpty(team1.GetProperty("partyId").GetString()));
    }

    [Fact]
    public async Task Party_context_shows_existing_party_to_a_plain_member()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (member, memberId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var ctx = await member.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/party-context");

        var team = ctx.GetProperty("teams")[0];
        Assert.Equal(partyId, team.GetProperty("partyId").GetGuid());
        Assert.False(team.GetProperty("isAdmin").GetBoolean());
        Assert.False(team.GetProperty("canForm").GetBoolean()); // members can't form, only view
        Assert.Equal(1, team.GetProperty("inCount").GetInt32());
    }

    // --- US2: participation request -------------------------------------------

    [Fact]
    public async Task Forming_notifies_every_team_member_but_not_the_creator_or_outsiders()
    {
        var (admin, _, _, adminEmail) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (m1, m1Id, _, m1Email) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, m1Id);
        var (outsider, _, _, outsiderEmail) = await NewUserAsync();
        var eventId = await CreateTeamsEventAsync(admin);
        Factory.EmailSender.Clear();

        await FormPartyAsync(admin, eventId, teamId);

        Assert.NotNull(Factory.EmailSender.LatestFor(m1Email));
        Assert.Null(Factory.EmailSender.LatestFor(adminEmail));
        Assert.Null(Factory.EmailSender.LatestFor(outsiderEmail));
    }

    [Fact]
    public async Task Team_requests_visible_to_member_but_404_for_non_member()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        await FormPartyAsync(admin, eventId, teamId);

        var mine = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/party-requests");
        Assert.Equal(1, mine.GetProperty("totalCount").GetInt32());

        var (outsider, _, _, _) = await NewUserAsync();
        var forbidden = await outsider.GetAsync($"/api/v1/teams/{slug}/party-requests");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    // --- US3: answer ----------------------------------------------------------

    [Fact]
    public async Task Member_joins_declines_rejoins_and_leaves()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (member, memberId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var join = await member.PostAsync($"/api/v1/parties/{partyId}/join", null);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}")).GetProperty("inCount").GetInt32());

        var decline = await member.PostAsync($"/api/v1/parties/{partyId}/decline", null);
        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);
        Assert.Equal(1, (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}")).GetProperty("inCount").GetInt32());

        // Decline is reversible.
        var rejoin = await member.PostAsync($"/api/v1/parties/{partyId}/join", null);
        Assert.Equal(HttpStatusCode.OK, rejoin.StatusCode);

        var leave = await member.PostAsync($"/api/v1/parties/{partyId}/leave", null);
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        Assert.Equal(1, (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}")).GetProperty("inCount").GetInt32());
    }

    [Fact]
    public async Task Non_team_member_cannot_join()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        var (outsider, _, _, _) = await NewUserAsync();

        var join = await outsider.PostAsync($"/api/v1/parties/{partyId}/join", null);

        Assert.Equal(HttpStatusCode.Forbidden, join.StatusCode);
    }

    [Fact]
    public async Task Roster_groups_split_in_declined_and_no_response()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (joiner, joinerId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, joinerId);
        var (decliner, declinerId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, declinerId);
        var (quiet, quietId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, quietId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        await joiner.PostAsync($"/api/v1/parties/{partyId}/join", null);
        await decliner.PostAsync($"/api/v1/parties/{partyId}/decline", null);

        var inGroup = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/members?group=in");
        var declined = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/members?group=declined");
        var noResponse = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/members?group=noResponse");

        Assert.Equal(2, inGroup.GetProperty("totalCount").GetInt32());   // admin + joiner
        Assert.Equal(1, declined.GetProperty("totalCount").GetInt32());  // decliner
        Assert.Equal(1, noResponse.GetProperty("totalCount").GetInt32()); // quiet
    }

    // --- US4: manage ----------------------------------------------------------

    [Fact]
    public async Task Admin_nudges_no_response_member_and_non_admin_cannot()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (member, memberId, _, memberEmail) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        Factory.EmailSender.Clear();

        var byNonAdmin = await member.PostAsync($"/api/v1/parties/{partyId}/members/{memberId}/nudge", null);
        Assert.Equal(HttpStatusCode.Forbidden, byNonAdmin.StatusCode);

        var byAdmin = await admin.PostAsync($"/api/v1/parties/{partyId}/members/{memberId}/nudge", null);
        Assert.Equal(HttpStatusCode.Accepted, byAdmin.StatusCode);
        Assert.NotNull(Factory.EmailSender.LatestFor(memberEmail));
    }

    [Fact]
    public async Task Remove_drops_from_party_but_keeps_team_membership()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (member, memberId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await member.PostAsync($"/api/v1/parties/{partyId}/join", null);

        var remove = await admin.DeleteAsync($"/api/v1/parties/{partyId}/members/{memberId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.UserId == memberId));
        Assert.False(await db.PartyMembers.AnyAsync(m => m.PartyId == partyId && m.UserId == memberId));
    }

    [Fact]
    public async Task Last_admin_cannot_leave()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var leave = await admin.PostAsync($"/api/v1/parties/{partyId}/leave", null);

        Assert.Equal(HttpStatusCode.Conflict, leave.StatusCode);
    }

    // --- US5: apply / withdraw ------------------------------------------------

    [Fact]
    public async Task Apply_free_lists_team_as_joined_and_withdraw_removes_it()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin, participationLimit: 4);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var apply = await admin.PostAsync($"/api/v1/parties/{partyId}/apply", null);
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        var applied = await apply.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Applied", applied.GetProperty("status").GetString());
        Assert.Equal("Joined", applied.GetProperty("appliedGroup").GetString());

        var joined = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/participants?group=joined");
        Assert.Equal(1, joined.GetProperty("totalCount").GetInt32());

        var withdraw = await admin.PostAsync($"/api/v1/parties/{partyId}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/participants?group=joined");
        Assert.Equal(0, after.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Apply_paid_awaits_approval()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin, paid: true, participationLimit: 4);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var apply = await admin.PostAsync($"/api/v1/parties/{partyId}/apply", null);

        Assert.Equal("AwaitingApproval", (await apply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("appliedGroup").GetString());
    }

    [Fact]
    public async Task Apply_when_full_waitlists_the_team()
    {
        var (org, _, _, _) = await NewUserAsync();
        var eventId = await CreateTeamsEventAsync(org, participationLimit: 1);

        var (a1, _, _, _) = await NewUserAsync();
        var (t1, _) = await CreateTeamAsync(a1);
        var p1 = await FormPartyAsync(a1, eventId, t1);
        (await a1.PostAsync($"/api/v1/parties/{p1}/apply", null)).EnsureSuccessStatusCode();

        var (a2, _, _, _) = await NewUserAsync();
        var (t2, _) = await CreateTeamAsync(a2);
        var p2 = await FormPartyAsync(a2, eventId, t2);
        var apply2 = await a2.PostAsync($"/api/v1/parties/{p2}/apply", null);

        Assert.Equal("Waitlisted", (await apply2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("appliedGroup").GetString());
    }

    [Fact]
    public async Task Non_party_admin_cannot_apply()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (member, memberId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var apply = await member.PostAsync($"/api/v1/parties/{partyId}/apply", null);

        Assert.Equal(HttpStatusCode.Forbidden, apply.StatusCode);
    }

    // --- US6: cap auto-close / reopen -----------------------------------------

    [Fact]
    public async Task Party_fills_to_cap_then_reopens_on_leave()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin); // admin = 1 In
        var eventId = await CreateTeamsEventAsync(admin, rosterCap: 5);

        var members = new List<(HttpClient Client, Guid Id)>();
        for (var i = 0; i < 5; i++)
        {
            var (c, id, _, _) = await NewUserAsync();
            await AddTeamMemberAsync(teamId, id);
            members.Add((c, id));
        }

        var partyId = await FormPartyAsync(admin, eventId, teamId);

        // Fill the remaining 4 spots (admin already holds 1 of 5).
        for (var i = 0; i < 4; i++)
        {
            (await members[i].Client.PostAsync($"/api/v1/parties/{partyId}/join", null)).EnsureSuccessStatusCode();
        }

        // Cap reached — the 5th member is refused (auto-closed, no waitlist).
        var full = await members[4].Client.PostAsync($"/api/v1/parties/{partyId}/join", null);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);

        // Someone leaves → auto-reopen → the previously-refused member gets in.
        (await members[0].Client.PostAsync($"/api/v1/parties/{partyId}/leave", null)).EnsureSuccessStatusCode();
        var reopened = await members[4].Client.PostAsync($"/api/v1/parties/{partyId}/join", null);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
    }

    [Fact]
    public async Task Concurrent_last_spot_joins_never_exceed_the_cap()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin); // 1 In (admin)
        var eventId = await CreateTeamsEventAsync(admin, rosterCap: 5);

        var clients = new List<HttpClient>();
        for (var i = 0; i < 8; i++)
        {
            var (c, id, _, _) = await NewUserAsync();
            await AddTeamMemberAsync(teamId, id);
            clients.Add(c);
        }

        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var results = await Task.WhenAll(clients.Select(c => c.PostAsync($"/api/v1/parties/{partyId}/join", null)));
        var ok = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(4, ok); // 5 cap − 1 admin already in

        var dto = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}");
        Assert.Equal(5, dto.GetProperty("inCount").GetInt32());
        Assert.True(dto.GetProperty("isFull").GetBoolean());
    }

    // --- US7: news ------------------------------------------------------------

    [Fact]
    public async Task Party_news_is_crew_only_and_admin_composed()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (crew, crewId, _, crewEmail) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, crewId);
        var (bench, benchId, _, _) = await NewUserAsync(); // team member who declines
        await AddTeamMemberAsync(teamId, benchId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        await crew.PostAsync($"/api/v1/parties/{partyId}/join", null);
        await bench.PostAsync($"/api/v1/parties/{partyId}/decline", null);
        Factory.EmailSender.Clear();

        var post = await admin.PostAsJsonAsync($"/api/v1/parties/{partyId}/news", new { body = "Meet 07:00 at the Aral." });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        // Crew member can read + was notified.
        var feed = await crew.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/news");
        Assert.Equal(1, feed.GetProperty("totalCount").GetInt32());
        Assert.NotNull(Factory.EmailSender.LatestFor(crewEmail));

        // A declined (non-crew) team member cannot read it.
        var benchRead = await bench.GetAsync($"/api/v1/parties/{partyId}/news");
        Assert.Equal(HttpStatusCode.NotFound, benchRead.StatusCode);

        // A non-admin crew member cannot post.
        var crewPost = await crew.PostAsJsonAsync($"/api/v1/parties/{partyId}/news", new { body = "nope" });
        Assert.Equal(HttpStatusCode.Forbidden, crewPost.StatusCode);
    }

    // --- US8: co-admins -------------------------------------------------------

    [Fact]
    public async Task Coadmin_invite_grants_admin_to_a_team_member()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (member, memberId, _, memberEmail) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        Factory.EmailSender.Clear();

        var invite = await admin.PostAsJsonAsync($"/api/v1/parties/{partyId}/invitations", new { userId = memberId });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var token = ExtractPartyInviteToken(Factory.EmailSender.LatestFor(memberEmail)!.HtmlBody);

        var accept = await member.PostAsync($"/api/v1/party-invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var dto = await member.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}");
        Assert.Equal("Admin", dto.GetProperty("myState").GetString());
    }

    [Fact]
    public async Task Coadmin_invite_refuses_a_non_team_member()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        var (outsider, outsiderId, _, _) = await NewUserAsync();

        var invite = await admin.PostAsJsonAsync($"/api/v1/parties/{partyId}/invitations", new { userId = outsiderId });

        Assert.Equal(HttpStatusCode.BadRequest, invite.StatusCode);
    }

    // --- US9: disband ---------------------------------------------------------

    [Fact]
    public async Task Disband_removes_party_withdraws_entry_and_keeps_team()
    {
        var (admin, adminId, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin, participationLimit: 4);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        (await admin.PostAsync($"/api/v1/parties/{partyId}/apply", null)).EnsureSuccessStatusCode();

        var disband = await admin.DeleteAsync($"/api/v1/parties/{partyId}");
        Assert.Equal(HttpStatusCode.NoContent, disband.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Parties.AnyAsync(p => p.Id == partyId));
        Assert.False(await db.EventSignups.AnyAsync(s => s.EventId == eventId && s.TeamId == teamId));
        Assert.True(await db.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.UserId == adminId));
    }

    [Fact]
    public async Task Non_admin_cannot_disband()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (member, memberId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var disband = await member.DeleteAsync($"/api/v1/parties/{partyId}");

        Assert.Equal(HttpStatusCode.Forbidden, disband.StatusCode);
    }

    // --- Edge: roster cap bounds + individuals events -------------------------

    [Fact]
    public async Task Roster_cap_below_five_is_refused_at_event_creation()
    {
        var (admin, _, _, _) = await NewUserAsync();

        var resp = await admin.PostAsJsonAsync("/api/v1/events", new
        {
            name = "Too small", type = "Tournament", description = "x",
            startsAt = "2026-09-05T09:00:00Z", endsAt = "2026-09-06T18:00:00Z",
            locationKind = "Virtual", virtualLink = "https://x.example/1",
            participantMode = "Teams", participationLimit = 8, rosterCap = 3,
            isPaid = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
