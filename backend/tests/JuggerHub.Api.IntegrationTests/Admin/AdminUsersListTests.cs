using System.Net;
using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Recognition;
using JuggerHub.Common;
using JuggerHub.Dtos.Admin;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Admin;

/// <summary>
/// Feature 013 US3 — the admin users list: search by name/@handle/team, status
/// filters (banned INCLUDED — the one surface where soft-deleted players remain
/// findable), pagination envelope, and the admin marker.
/// </summary>
[Collection("AdminArea")]
public sealed class AdminUsersListTests
{
    private readonly JuggerHubApiFactory _factory;

    public AdminUsersListTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Users_list_requires_platform_admin()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/admin/users")).StatusCode);

        var (player, _, _, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync("/api/v1/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Search_matches_name_handle_and_team_and_filters_by_status()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);

        var (playerClient, _, handle, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        var (_, suspendedId, suspendedHandle, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        var (_, bannedId, bannedHandle, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        await AdminAreaTestSupport.SetStatusAsync(_factory, suspendedId, AccountStatus.Suspended);
        await AdminAreaTestSupport.SetStatusAsync(_factory, bannedId, AccountStatus.Banned);

        // The active player founds a team with a unique name (team-name search fixture).
        var teamName = $"Team {handle[..8]}";
        var teamSlug = $"t{Guid.NewGuid():N}"[..18];
        var teamResp = await playerClient.PostAsJsonAsync("/api/v1/teams",
            new { name = teamName, slug = teamSlug, type = "CityTeam", city = "Berlin" });
        teamResp.EnsureSuccessStatusCode();

        // By @handle (leading @ tolerated).
        var byHandle = await SearchAsync(admin, $"q=@{handle}");
        var row = Assert.Single(byHandle.Items);
        Assert.Equal(handle, row.Handle);
        Assert.Contains(teamName, row.Teams);
        Assert.False(row.IsAdmin);
        Assert.Equal(AccountStatus.Active, row.Status);

        // By display name (defaults to the handle at registration → same match, via name path).
        var byName = await SearchAsync(admin, $"q={handle[..12]}");
        Assert.Contains(byName.Items, i => i.Handle == handle);

        // By team name.
        var byTeam = await SearchAsync(admin, $"q={Uri.EscapeDataString(teamName)}");
        Assert.Contains(byTeam.Items, i => i.Handle == handle);

        // Status filters — banned players ARE findable here (and only here).
        var suspendedOnly = await SearchAsync(admin, $"q={suspendedHandle}&status=Suspended");
        Assert.Equal(suspendedHandle, Assert.Single(suspendedOnly.Items).Handle);

        var bannedOnly = await SearchAsync(admin, $"q={bannedHandle}&status=Banned");
        Assert.Equal(bannedHandle, Assert.Single(bannedOnly.Items).Handle);

        // Combined: an Active filter must not return the suspended player.
        var activeFiltered = await SearchAsync(admin, $"q={suspendedHandle}&status=Active");
        Assert.Empty(activeFiltered.Items);

        // The admin themself carries the marker.
        var adminRow = await SearchAsync(admin, "status=Active&take=100");
        Assert.Contains(adminRow.Items, i => i.IsAdmin);
    }

    [Fact]
    public async Task Pagination_envelope_stays_truthful()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        await AdminAreaTestSupport.PlayerClientAsync(_factory);
        await AdminAreaTestSupport.PlayerClientAsync(_factory);

        var pageOne = await SearchAsync(admin, "skip=0&take=2");
        Assert.Equal(2, pageOne.Items.Count);
        Assert.Equal(0, pageOne.Skip);
        Assert.Equal(2, pageOne.Take);
        Assert.True(pageOne.TotalCount >= 3);

        var pageTwo = await SearchAsync(admin, "skip=2&take=2");
        Assert.Equal(2, pageTwo.Skip);
        Assert.Equal(pageOne.TotalCount, pageTwo.TotalCount);
        // Deterministic ordering: no row appears on both pages.
        Assert.Empty(pageOne.Items.Select(i => i.Handle).Intersect(pageTwo.Items.Select(i => i.Handle)));
    }

    private static async Task<PagedResult<AdminUserListItemDto>> SearchAsync(HttpClient admin, string query)
    {
        var resp = await admin.GetAsync($"/api/v1/admin/users?{query}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<PagedResult<AdminUserListItemDto>>(AdminAreaTestSupport.Json))!;
    }
}
