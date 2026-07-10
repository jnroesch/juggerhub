using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Recognition;

/// <summary>
/// Feature 012 US1 — the admin subject-awards read that backs the grant/revoke UI: a subject's
/// current badges + achievements with award id, admin note, and granter; access probe; 404s.
/// </summary>
[Collection("Recognition")]
public sealed class AdminSubjectAwardsTests
{
    private readonly JuggerHubApiFactory _factory;

    public AdminSubjectAwardsTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Access_probe_returns_200_for_admin()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var resp = await admin.GetAsync("/api/v1/admin/access");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(dto.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Player_awards_lists_badges_and_achievements_with_note_and_granter()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var badgeId = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges", name: "Fair play");
        var achId = await RecognitionTestSupport.CreateDefinitionAsync(admin, "achievements", name: "Champion");

        await admin.PostAsJsonAsync($"/api/v1/admin/badges/{badgeId}/awards",
            new { playerHandle = handle, note = "great sportsmanship" });
        await admin.PostAsJsonAsync($"/api/v1/admin/achievements/{achId}/awards",
            new { playerHandle = handle, contextYear = 2026 });

        var resp = await admin.GetAsync($"/api/v1/admin/players/{handle}/awards");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var badge = dto.GetProperty("badges").EnumerateArray().Single();
        Assert.Equal("Fair play", badge.GetProperty("name").GetString());
        Assert.Equal("great sportsmanship", badge.GetProperty("note").GetString());
        Assert.False(string.IsNullOrEmpty(badge.GetProperty("grantedByName").GetString()));
        Assert.False(badge.GetProperty("awardId").GetGuid() == Guid.Empty);

        var ach = dto.GetProperty("achievements").EnumerateArray().Single();
        Assert.Equal("Champion", ach.GetProperty("name").GetString());
        Assert.Equal(2026, ach.GetProperty("contextYear").GetInt32());
    }

    [Fact]
    public async Task Unknown_player_is_404()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var resp = await admin.GetAsync($"/api/v1/admin/players/nobody{Guid.NewGuid():N}/awards");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Team_awards_reflect_grant_and_revoke()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (userClient, _, _, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var slug = await RecognitionTestSupport.CreateTeamAsync(userClient);
        var badgeId = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges");

        var grant = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{badgeId}/awards", new { teamSlug = slug });
        var awardId = (await grant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var before = await (await admin.GetAsync($"/api/v1/admin/teams/{slug}/awards")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(before.GetProperty("badges").EnumerateArray());

        await admin.DeleteAsync($"/api/v1/admin/badges/awards/{awardId}");

        var after = await (await admin.GetAsync($"/api/v1/admin/teams/{slug}/awards")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(after.GetProperty("badges").EnumerateArray());
    }
}
