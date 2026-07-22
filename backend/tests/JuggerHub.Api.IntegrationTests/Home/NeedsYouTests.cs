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
