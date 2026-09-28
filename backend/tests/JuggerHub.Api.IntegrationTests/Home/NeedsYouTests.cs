using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Home;

/// <summary>
/// Integration tests for the "Needs you" actionable block (feature 025, US1). Verifies that items are
/// aggregated from their authoritative source domains (not the notification cache), that a resolved
/// source leaves the block, and that a viewer with nothing pending gets an empty block.
/// </summary>
[Collection("Home")]
public sealed class NeedsYouTests
{
    private static readonly DateTime Soon = DateTime.UtcNow.AddDays(3);

    private readonly JuggerHubApiFactory _factory;

    public NeedsYouTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task NeedsYou_is_empty_when_nothing_is_pending()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Quiet team");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.Empty(home.GetProperty("needsYou").EnumerateArray());
    }

    [Fact]
    public async Task Pending_targeted_team_invite_surfaces_and_clears_when_consumed()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, teamSlug) = await HomeTestSupport.SeedTeamAsync(_factory, "Bloodhounds");

        var token = await SeedTeamInviteAsync(teamId, userId, InvitationStatus.Pending);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var item = home.GetProperty("needsYou").EnumerateArray()
            .First(i => i.GetProperty("kind").GetString() == "TeamInvite");
        Assert.Equal(token, item.GetProperty("id").GetString());
        Assert.Equal(teamSlug, item.GetProperty("linkTarget").GetString());

        // Consuming the invite at the source (authoritative) removes it — a stale notification could not.
        await HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            await db.TeamInvitations.Where(i => i.Token == token)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Status, InvitationStatus.Accepted)
                    .SetProperty(i => i.ModifiedDate, DateTime.UtcNow));
        });

        var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.DoesNotContain(after.GetProperty("needsYou").EnumerateArray(),
            i => i.GetProperty("kind").GetString() == "TeamInvite");
    }

    [Fact]
    public async Task Party_participation_request_surfaces_until_the_viewer_answers()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Rooks");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);

        var eventId = await HomeTestSupport.SeedEventAsync(_factory, "League match", Soon, Soon.AddHours(2), ParticipantMode.Teams);
        var partyId = await SeedPartyAsync(teamId, eventId, userId);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var item = home.GetProperty("needsYou").EnumerateArray()
            .First(i => i.GetProperty("kind").GetString() == "PartyRequest");
        Assert.Equal(partyId.ToString(), item.GetProperty("id").GetString());

        // Once the viewer answers (a PartyMember row exists), the request is no longer "no response".
        await HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            db.PartyMembers.Add(new PartyMember { PartyId = partyId, UserId = userId, Status = PartyMemberStatus.In });
            await db.SaveChangesAsync();
        });

        var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.DoesNotContain(after.GetProperty("needsYou").EnumerateArray(),
            i => i.GetProperty("kind").GetString() == "PartyRequest");
    }

    [Fact]
    public async Task Marketplace_invite_surfaces_as_an_actionable_item()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Recruiters");
        var eventId = await HomeTestSupport.SeedEventAsync(_factory, "Summer Slam", Soon, Soon.AddHours(2), ParticipantMode.Teams);
        var partyId = await SeedPartyAsync(teamId, eventId, userId);
        var requestId = await SeedMarketInviteAsync(partyId, userId);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var item = home.GetProperty("needsYou").EnumerateArray()
            .First(i => i.GetProperty("kind").GetString() == "MarketInvite");
        Assert.Equal(requestId.ToString(), item.GetProperty("id").GetString());
        Assert.Equal(eventId.ToString(), item.GetProperty("linkTarget").GetString());
    }

    // --- Feature 058: join requests, and names instead of sentences -----------------------

    [Fact]
    public async Task An_admin_sees_each_waiting_request_once()
    {
        var (admin, adminId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, teamSlug) = await HomeTestSupport.SeedTeamAsync(_factory, "Hamburg Hammers");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, adminId, TeamRole.Admin);
        var (_, playerId) = await HomeTestSupport.NewUserAsync(_factory);
        var requestId = await SeedJoinRequestAsync(teamId, playerId);
        var (handle, displayName) = await ProfileOfAsync(playerId);

        var item = Assert.Single(await JoinRequestItemsAsync(admin));

        Assert.Equal(requestId.ToString(), item.GetProperty("id").GetString());
        Assert.Equal(handle, item.GetProperty("linkTarget").GetString());
        var p = item.GetProperty("params");
        Assert.Equal(displayName, p.GetProperty("playerName").GetString());
        Assert.Equal("Hamburg Hammers", p.GetProperty("teamName").GetString());
        Assert.Equal(teamSlug, p.GetProperty("teamSlug").GetString());
    }

    [Fact]
    public async Task Across_every_team_they_administer()
    {
        var (admin, adminId) = await HomeTestSupport.NewUserAsync(_factory);
        foreach (var name in new[] { "Team One", "Team Two" })
        {
            var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, name);
            await HomeTestSupport.AddMemberAsync(_factory, teamId, adminId, TeamRole.Admin);
            var (_, playerId) = await HomeTestSupport.NewUserAsync(_factory);
            await SeedJoinRequestAsync(teamId, playerId);
        }

        var teams = (await JoinRequestItemsAsync(admin))
            .Select(i => i.GetProperty("params").GetProperty("teamName").GetString())
            .Order()
            .ToArray();
        Assert.Equal(new[] { "Team One", "Team Two" }, teams);
    }

    [Fact]
    public async Task Members_and_the_player_see_no_join_request()
    {
        var (member, memberId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Quiet Admins");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, memberId);
        var (player, playerId) = await HomeTestSupport.NewUserAsync(_factory);
        await SeedJoinRequestAsync(teamId, playerId);

        Assert.Empty(await JoinRequestItemsAsync(member));
        Assert.Empty(await JoinRequestItemsAsync(player));
    }

    [Fact]
    public async Task An_answered_or_banned_request_leaves_needs_you()
    {
        var (admin, adminId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Busy Admins");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, adminId, TeamRole.Admin);
        var (_, answeredId) = await HomeTestSupport.NewUserAsync(_factory);
        var (_, bannedId) = await HomeTestSupport.NewUserAsync(_factory);
        var answered = await SeedJoinRequestAsync(teamId, answeredId);
        await SeedJoinRequestAsync(teamId, bannedId);
        Assert.Equal(2, (await JoinRequestItemsAsync(admin)).Count);

        await HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            await db.TeamJoinRequests.Where(r => r.Id == answered)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, JoinRequestStatus.Declined)
                    .SetProperty(r => r.ModifiedDate, DateTime.UtcNow));
            await db.Users.Where(u => u.Id == bannedId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, AccountStatus.Banned));
        });

        Assert.Empty(await JoinRequestItemsAsync(admin));
    }

    [Fact]
    public async Task Every_kind_carries_names_not_sentences()
    {
        // GH #141's defect class: the server used to send "Hamburg Hammers invited you" in English
        // to every viewer. Now it sends names; the client supplies the words in the viewer's language.
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Bloodhounds");
        await SeedTeamInviteAsync(teamId, userId, InvitationStatus.Pending);
        var eventId = await HomeTestSupport.SeedEventAsync(_factory, "Summer Slam", Soon, Soon.AddHours(2), ParticipantMode.Teams);
        var partyId = await SeedPartyAsync(teamId, eventId, userId);
        await SeedMarketInviteAsync(partyId, userId);

        var items = (await client.GetFromJsonAsync<JsonElement>("/api/v1/home")).GetProperty("needsYou").EnumerateArray().ToList();

        var invite = items.First(i => i.GetProperty("kind").GetString() == "TeamInvite");
        Assert.Equal("Bloodhounds", invite.GetProperty("params").GetProperty("teamName").GetString());
        var market = items.First(i => i.GetProperty("kind").GetString() == "MarketInvite");
        Assert.Equal("Bloodhounds", market.GetProperty("params").GetProperty("teamName").GetString());
        Assert.Equal("Summer Slam", market.GetProperty("params").GetProperty("eventName").GetString());
        Assert.All(items, i =>
        {
            Assert.False(i.TryGetProperty("title", out _), "An item still carries a server-built title.");
            Assert.False(i.TryGetProperty("context", out _), "An item still carries a server-built context line.");
        });
    }

    private static async Task<List<JsonElement>> JoinRequestItemsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/v1/home")).GetProperty("needsYou").EnumerateArray()
            .Where(i => i.GetProperty("kind").GetString() == "JoinRequest")
            .ToList();

    private Task<Guid> SeedJoinRequestAsync(Guid teamId, Guid userId) =>
        HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            var request = new TeamJoinRequest { TeamId = teamId, UserId = userId, Status = JoinRequestStatus.Pending };
            db.TeamJoinRequests.Add(request);
            await db.SaveChangesAsync();
            return request.Id;
        });

    private async Task<(string Handle, string DisplayName)> ProfileOfAsync(Guid userId)
    {
        var profile = await HomeTestSupport.WithDbAsync(_factory, db => db.PlayerProfiles
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Handle, p.DisplayName })
            .SingleAsync());
        return (profile.Handle, profile.DisplayName);
    }

    // --- Seed helpers ---------------------------------------------------------

    private Task<string> SeedTeamInviteAsync(Guid teamId, Guid targetUserId, InvitationStatus status) =>
        HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            var token = "tok-" + Guid.NewGuid().ToString("N");
            db.TeamInvitations.Add(new TeamInvitation
            {
                TeamId = teamId,
                Kind = InvitationKind.Targeted,
                Token = token,
                Status = status,
                ExpiresDate = DateTime.UtcNow.AddDays(7),
                CreatedByUserId = targetUserId,
                TargetUserId = targetUserId,
            });
            await db.SaveChangesAsync();
            return token;
        });

    private Task<Guid> SeedPartyAsync(Guid teamId, Guid eventId, Guid createdByUserId) =>
        HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            var party = new Party
            {
                TeamId = teamId,
                EventId = eventId,
                RosterCap = 8,
                Status = PartyStatus.Open,
                CreatedByUserId = createdByUserId,
            };
            db.Parties.Add(party);
            await db.SaveChangesAsync();
            return party.Id;
        });

    private Task<Guid> SeedMarketInviteAsync(Guid partyId, Guid userId) =>
        HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            var req = new MarketRequest
            {
                PartyId = partyId,
                UserId = userId,
                Direction = MarketRequestDirection.Invite,
                Positions = [Pompfe.Langpompfe],
                Status = MarketRequestStatus.Pending,
                CreatedByUserId = userId,
            };
            db.MarketRequests.Add(req);
            await db.SaveChangesAsync();
            return req.Id;
        });
}
