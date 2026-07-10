using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Admin;

/// <summary>
/// Feature 014 admin teams browse: search lists teams with member/award counts, detail returns a
/// team's identity, an unknown slug is 404, and the whole area is gated by the PlatformAdmin policy.
/// </summary>
[Collection("AdminArea")]
public sealed class AdminTeamsTests
{
    private readonly JuggerHubApiFactory _factory;

    public AdminTeamsTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Search_lists_the_team_with_counts_and_detail_returns_identity()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var (player, _, _, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);

        var slug = $"bison{Guid.NewGuid():N}"[..16];
        var name = $"bison{Guid.NewGuid():N}"[..16];
        (await player.PostAsJsonAsync("/api/v1/teams", new { name, slug, type = "CityTeam", city = "Berlin" }))
            .EnsureSuccessStatusCode();

        // Search by (unique) name finds it with counts — the creator is an auto-member.
        var search = await admin.GetAsync($"/api/v1/admin/teams?q={name}");
        search.EnsureSuccessStatusCode();
        var page = await search.Content.ReadFromJsonAsync<JsonElement>();
        var found = page.GetProperty("items").EnumerateArray().Single(t => t.GetProperty("slug").GetString() == slug);
        Assert.Equal(name, found.GetProperty("name").GetString());
        Assert.Equal("Berlin", found.GetProperty("city").GetString());
        Assert.True(found.GetProperty("memberCount").GetInt32() >= 1);
        Assert.Equal(0, found.GetProperty("awardCount").GetInt32());

        // Detail by slug.
        var detail = await admin.GetAsync($"/api/v1/admin/teams/{slug}");
        detail.EnsureSuccessStatusCode();
        var dto = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(name, dto.GetProperty("name").GetString());
        Assert.Equal("CityTeam", dto.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Detail_for_unknown_slug_is_404()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var resp = await admin.GetAsync($"/api/v1/admin/teams/nope-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Teams_area_is_gated_to_admins()
    {
        var (player, _, _, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        var forbidden = await player.GetAsync("/api/v1/admin/teams");
        Assert.True(forbidden.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);

        var anon = _factory.CreateClient();
        var unauth = await anon.GetAsync("/api/v1/admin/teams");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);
    }
}
