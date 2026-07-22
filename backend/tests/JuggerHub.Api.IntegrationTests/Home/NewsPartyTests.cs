using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Home;

/// <summary>
/// Integration tests for party news in the Home News merge (feature 025, US3): party posts appear
/// only for `In` members, tagged with the "party" source, alongside team and event news.
/// </summary>
[Collection("Home")]
public sealed class NewsPartyTests
{
    private static readonly DateTime Soon = DateTime.UtcNow.AddDays(3);

    private readonly JuggerHubApiFactory _factory;

    public NewsPartyTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Party_news_appears_for_an_in_member_tagged_party()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Rheinfire");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);
        var eventId = await HomeTestSupport.SeedEventAsync(_factory, "Summer Slam", Soon, Soon.AddHours(2), ParticipantMode.Teams);

        await SeedPartyNewsAsync(teamId, eventId, userId, memberIn: true, body: "Bring cash for the ref");

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var news = home.GetProperty("news").EnumerateArray().ToList();

        var post = news.First(n => n.GetProperty("body").GetString() == "Bring cash for the ref");
        Assert.Equal("party", post.GetProperty("source").GetString());
        Assert.Equal(eventId.ToString(), post.GetProperty("sourceSlugOrId").GetString());
    }

    [Fact]
    public async Task Party_news_is_hidden_from_a_non_member_of_the_party()
    {
        var (client, userId) = await HomeTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await HomeTestSupport.SeedTeamAsync(_factory, "Rooks");
        await HomeTestSupport.AddMemberAsync(_factory, teamId, userId);
        var eventId = await HomeTestSupport.SeedEventAsync(_factory, "Autumn Clash", Soon, Soon.AddHours(2), ParticipantMode.Teams);

        // A party of the viewer's team, but the viewer has not joined it (no In row) — its news is private.
        await SeedPartyNewsAsync(teamId, eventId, userId, memberIn: false, body: "Crew-only secret");

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.DoesNotContain(home.GetProperty("news").EnumerateArray(),
            n => n.GetProperty("body").GetString() == "Crew-only secret");
    }

    private Task SeedPartyNewsAsync(Guid teamId, Guid eventId, Guid userId, bool memberIn, string body) =>
        HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            var party = new Party
            {
                TeamId = teamId,
                EventId = eventId,
                RosterCap = 8,
                Status = PartyStatus.Open,
                CreatedByUserId = userId,
            };
            db.Parties.Add(party);
            await db.SaveChangesAsync();

            if (memberIn)
            {
                db.PartyMembers.Add(new PartyMember { PartyId = party.Id, UserId = userId, Status = PartyMemberStatus.In });
            }

            db.PartyNewsPosts.Add(new PartyNewsPost { PartyId = party.Id, AuthorUserId = userId, Body = body });
            await db.SaveChangesAsync();
        });
}
