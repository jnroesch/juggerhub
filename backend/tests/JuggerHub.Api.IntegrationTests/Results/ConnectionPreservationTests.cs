using System.Net;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>
/// Research R8 / FR-027: on a past tournament no team has a JuggerHub sign-up, so a platform admin
/// connects placements after checking by hand. An event admin's later edit must keep exactly those
/// connections — never drop one silently, and never let one be re-pointed or created by the event
/// admin through it.
/// </summary>
[Collection("Results")]
public sealed class ConnectionPreservationTests : ResultsTestSupport
{
    public ConnectionPreservationTests(JuggerHubApiFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// A started tournament whose placement "Rigor" is connected, by the platform admin, to a team
    /// that has no JuggerHub sign-up for it; plus a plain-name second place.
    /// </summary>
    private async Task<(HttpClient EventAdmin, Guid EventId, Guid ConnectedRowId, Guid PlainRowId, Guid TeamId, Guid PlatformAdminId)> SeedAsync()
    {
        var (eventAdmin, _, _, _) = await NewUserAsync();
        var eventId = await CreateTournamentAsync(eventAdmin);
        await StartEventAsync(eventId);
        await ReadJsonAsync(await SaveRankingAsync(eventAdmin, eventId, (null, 1, "Rigor", null), (null, 2, "Kiel Guests", null)));

        var (teamId, _) = await NewTeamAsync();
        var (_, platformAdminId) = await PlatformAdminClientAsync();
        var (connectedRowId, plainRowId) = await WithDbAsync(async db =>
        {
            var rows = await db.TournamentPlacements.Where(p => p.TournamentResult.EventId == eventId).ToListAsync();
            var rigor = rows.Single(p => p.SourceName == "Rigor");
            rigor.TeamId = teamId;
            rigor.Name = "Rheinfeuer";
            rigor.ConnectedByUserId = platformAdminId;
            rigor.ConnectedAt = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
            return (rigor.Id, rows.Single(p => p.SourceName == "Kiel Guests").Id);
        });

        return (eventAdmin, eventId, connectedRowId, plainRowId, teamId, platformAdminId);
    }

    [Fact]
    public async Task Re_saving_with_the_same_row_keeps_the_platform_admins_connection()
    {
        var (eventAdmin, eventId, connectedRowId, plainRowId, teamId, platformAdminId) = await SeedAsync();

        // The event admin fixes a typo in the OTHER row and saves the whole ranking back.
        await ReadJsonAsync(await SaveRankingAsync(eventAdmin, eventId,
            (connectedRowId, 1, "Rigor", teamId), (plainRowId, 2, "Kiel Guests (fixed)", null)));

        var row = await WithDbAsync(db => db.TournamentPlacements.SingleAsync(p => p.Id == connectedRowId));
        Assert.Equal(teamId, row.TeamId);
        Assert.Equal(platformAdminId, row.ConnectedByUserId);
        Assert.Equal("Rheinfeuer", row.Name);
    }

    [Fact]
    public async Task The_connection_cannot_be_moved_to_another_row()
    {
        var (eventAdmin, eventId, connectedRowId, plainRowId, teamId, _) = await SeedAsync();

        var response = await SaveRankingAsync(eventAdmin, eventId,
            (connectedRowId, 1, "Rigor", null), (plainRowId, 2, "Kiel Guests", teamId));

        await AssertStatusAsync(response, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Omitting_the_row_removes_it()
    {
        var (eventAdmin, eventId, connectedRowId, plainRowId, _, _) = await SeedAsync();

        await ReadJsonAsync(await SaveRankingAsync(eventAdmin, eventId, (plainRowId, 1, "Kiel Guests", null)));

        Assert.False(await WithDbAsync(db => db.TournamentPlacements.AnyAsync(p => p.Id == connectedRowId)));
    }

    [Fact]
    public async Task Changing_the_row_to_a_signed_up_team_attributes_it_to_the_event_admin()
    {
        var (eventAdmin, eventId, connectedRowId, plainRowId, _, platformAdminId) = await SeedAsync();
        var (signedUp, _) = await NewTeamAsync();
        await SeedSignupAsync(eventId, signedUp);

        await ReadJsonAsync(await SaveRankingAsync(eventAdmin, eventId,
            (connectedRowId, 1, "Rigor", signedUp), (plainRowId, 2, "Kiel Guests", null)));

        var row = await WithDbAsync(db => db.TournamentPlacements.SingleAsync(p => p.Id == connectedRowId));
        Assert.Equal(signedUp, row.TeamId);
        Assert.NotEqual(platformAdminId, row.ConnectedByUserId);
    }
}
