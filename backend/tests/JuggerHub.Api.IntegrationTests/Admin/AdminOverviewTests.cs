using System.Net;
using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Recognition;
using JuggerHub.Dtos.Admin;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Admin;

/// <summary>
/// Feature 013 US2 — the admin landing aggregate. The collection's database is shared
/// across admin-area suites, so count assertions are DELTAS around this test's own
/// fixtures, never absolutes.
/// </summary>
[Collection("AdminArea")]
public sealed class AdminOverviewTests
{
    private readonly JuggerHubApiFactory _factory;

    public AdminOverviewTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Overview_requires_platform_admin()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/admin/overview")).StatusCode);

        var (player, _, _, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync("/api/v1/admin/overview")).StatusCode);
    }

    [Fact]
    public async Task Overview_counts_and_lists_reflect_account_states_and_grants()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);

        var before = await GetOverviewAsync(admin);

        // Fixtures: three fresh players — one stays active, one is suspended, one is banned.
        var (_, _, activeHandle, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        var (_, suspendedId, _, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        var (_, bannedId, _, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        await AdminAreaTestSupport.SetStatusAsync(_factory, suspendedId, AccountStatus.Suspended);
        await AdminAreaTestSupport.SetStatusAsync(_factory, bannedId, AccountStatus.Banned);

        // An event inside the 30-day window (direct insert — the event API is not under test).
        await AdminAreaTestSupport.WithDbAsync(_factory, async db =>
        {
            db.Events.Add(new Event
            {
                Name = "Overview window event",
                Description = "Fixture",
                Location = "Berlin",
                StartsAt = DateTime.UtcNow.AddDays(-5),
            });
            await db.SaveChangesAsync();
        });

        // A badge grant to the active player (through the real 012 admin API).
        var definitionId = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges", name: "Fair play");
        var grant = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{definitionId}/awards",
            new { playerHandle = activeHandle, note = "overview test" });
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);

        var after = await GetOverviewAsync(admin);

        // Players: +3 registered, −1 banned (banned accounts are "removed", not counted).
        Assert.Equal(before.Players + 2, after.Players);
        // Suspended: +1.
        Assert.Equal(before.Suspended + 1, after.Suspended);
        // Events in the last 30 days: +1.
        Assert.Equal(before.EventsLast30Days + 1, after.EventsLast30Days);
        // Teams unchanged by this test.
        Assert.Equal(before.Teams, after.Teams);

        // New players this week: capped and containing our fresh (non-banned) player.
        Assert.True(after.NewPlayers.Count <= 5);
        Assert.Contains(after.NewPlayers, p => p.Handle == activeHandle);

        // Recently granted: the badge shows with kind, name, subject, and attribution.
        var recent = Assert.Single(after.RecentGrants, g => g.SubjectHandle == activeHandle);
        Assert.Equal("Badge", recent.Kind);
        Assert.Equal("Fair play", recent.Name);
        Assert.NotEqual("—", recent.GrantedByDisplayName);
    }

    private static async Task<AdminOverviewDto> GetOverviewAsync(HttpClient admin)
    {
        var resp = await admin.GetAsync("/api/v1/admin/overview");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<AdminOverviewDto>())!;
    }
}
