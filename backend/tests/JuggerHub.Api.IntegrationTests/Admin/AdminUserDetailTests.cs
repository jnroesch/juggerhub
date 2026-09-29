using System.Net;
using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Dtos.Admin;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Admin;

/// <summary>
/// GH #378 — "last active" on the admin player detail. It used to read only event participations,
/// which nothing in the product writes, so every player who had signed in showed "—". It is now the
/// newer of the newest session record (sign-in or token rotation) and the newest participation.
/// Every expected value is read back from the database, so the comparisons are exact.
/// </summary>
[Collection("AdminArea")]
public sealed class AdminUserDetailTests
{
    private readonly JuggerHubApiFactory _factory;

    public AdminUserDetailTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_player_who_signed_in_reads_as_active_without_any_event_history()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var (_, playerId, handle, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);

        var detail = await DetailAsync(admin, handle);

        Assert.Empty(detail.RecentActivity);
        Assert.NotNull(detail.LastActiveAt);
        Assert.Equal(await NewestSessionAsync(playerId), detail.LastActiveAt);
    }

    [Fact]
    public async Task Last_active_is_the_newer_of_session_and_participation_and_empty_without_either()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var (_, playerId, handle, email) = await AdminAreaTestSupport.PlayerClientAsync(_factory);

        // The sign-in happened ten days ago; a participation recorded three days ago is newer.
        await AdminAreaTestSupport.WithDbAsync(_factory, db => db.RefreshTokens
            .Where(t => t.UserId == playerId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedDate, DateTime.UtcNow.AddDays(-10))));
        var participationAt = await AddParticipationAsync(playerId, DateTime.UtcNow.AddDays(-3));

        Assert.Equal(participationAt, (await DetailAsync(admin, handle)).LastActiveAt);

        // Signing in again is newer than the participation.
        var client = _factory.CreateClient();
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        var signedInAt = await NewestSessionAsync(playerId);

        Assert.True(signedInAt > participationAt);
        Assert.Equal(signedInAt, (await DetailAsync(admin, handle)).LastActiveAt);

        // Once the retention sweep has removed the session rows and there is no participation,
        // there is nothing to derive it from.
        await AdminAreaTestSupport.WithDbAsync(_factory, async db =>
        {
            await db.RefreshTokens.Where(t => t.UserId == playerId).ExecuteDeleteAsync();
            await db.EventParticipations.Where(ep => ep.Profile.UserId == playerId).ExecuteDeleteAsync();
        });

        Assert.Null((await DetailAsync(admin, handle)).LastActiveAt);
    }

    private static async Task<AdminUserDetailDto> DetailAsync(HttpClient admin, string handle)
    {
        var resp = await admin.GetAsync($"/api/v1/admin/users/{handle}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<AdminUserDetailDto>(AdminAreaTestSupport.Json))!;
    }

    private Task<DateTime> NewestSessionAsync(Guid userId) =>
        AdminAreaTestSupport.WithDbAsync(_factory, db =>
            db.RefreshTokens.Where(t => t.UserId == userId).MaxAsync(t => t.CreatedDate));

    /// <summary>Records a participation and dates it; returns the stored date.</summary>
    private Task<DateTime> AddParticipationAsync(Guid userId, DateTime recordedAt) =>
        AdminAreaTestSupport.WithDbAsync(_factory, async db =>
        {
            var profileId = await db.PlayerProfiles.IgnoreQueryFilters()
                .Where(p => p.UserId == userId).Select(p => p.Id).SingleAsync();
            var ev = new Event
            {
                Name = "Last-active fixture",
                Description = "Last-active fixture",
                StartsAt = recordedAt,
                EndsAt = recordedAt.AddHours(8),
                Location = "Berlin",
            };
            var participation = new EventParticipation { ProfileId = profileId, Event = ev, TeamLabel = "Guest" };
            db.EventParticipations.Add(participation);
            await db.SaveChangesAsync();

            // The audit interceptor stamps CreatedDate with "now" on insert; move it afterwards.
            await db.EventParticipations.Where(ep => ep.Id == participation.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(ep => ep.CreatedDate, recordedAt));
            return await db.EventParticipations.Where(ep => ep.Id == participation.Id)
                .Select(ep => ep.CreatedDate).SingleAsync();
        });
}
