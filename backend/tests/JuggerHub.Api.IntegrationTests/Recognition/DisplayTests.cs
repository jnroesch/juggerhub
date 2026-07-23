using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Recognition;

/// <summary>
/// Feature 012 US2 — earned badges/achievements surface (active only) on the public profile and
/// team page payloads, and disappear on revoke. Confirms the public field set excludes the note.
/// </summary>
[Collection("Recognition")]
public sealed class DisplayTests
{
    private readonly JuggerHubApiFactory _factory;

    public DisplayTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_profile_shows_active_awards_and_hides_revoked_without_note()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var badgeId = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges", name: "Beta Tester");
        var achId = await RecognitionTestSupport.CreateDefinitionAsync(admin, "achievements", name: "Champion");

        var grantBadge = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{badgeId}/awards",
            new { playerHandle = handle, note = "secret admin note" });
        var badgeAwardId = (await grantBadge.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/admin/achievements/{achId}/awards",
            new { playerHandle = handle, contextYear = 2026, contextLabel = "Nationals" });

        // Read the public profile as an authenticated viewer (the admin); the public projection
        // strips the note for every viewer (feature 026 — profiles aren't anonymous by default).
        var profile = await (await admin.GetAsync($"/api/v1/profiles/{handle}")).Content.ReadFromJsonAsync<JsonElement>();

        var badge = profile.GetProperty("badges").EnumerateArray().Single();
        Assert.Equal("Beta Tester", badge.GetProperty("name").GetString());
        Assert.Equal(badgeId, badge.GetProperty("definitionId").GetGuid());
        // The admin note is NOT exposed publicly.
        Assert.False(badge.TryGetProperty("note", out _));

        var ach = profile.GetProperty("achievements").EnumerateArray().Single();
        Assert.Equal(2026, ach.GetProperty("contextYear").GetInt32());
        Assert.Equal("Nationals", ach.GetProperty("contextLabel").GetString());

        // Revoke the badge → it disappears from the public profile.
        await admin.DeleteAsync($"/api/v1/admin/badges/awards/{badgeAwardId}");
        var after = await (await admin.GetAsync($"/api/v1/profiles/{handle}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(after.GetProperty("badges").EnumerateArray());
    }

    [Fact]
    public async Task Team_public_page_shows_team_awards()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (userClient, _, _, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var slug = await RecognitionTestSupport.CreateTeamAsync(userClient);
        var badgeId = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges", name: "Founding club");

        await admin.PostAsJsonAsync($"/api/v1/admin/badges/{badgeId}/awards", new { teamSlug = slug });

        // Feature 026: the team public view is authenticated-only; read it as the signed-in owner.
        var page = await (await userClient.GetAsync($"/api/v1/teams/{slug}/public")).Content.ReadFromJsonAsync<JsonElement>();
        var badge = page.GetProperty("badges").EnumerateArray().Single();
        Assert.Equal("Founding club", badge.GetProperty("name").GetString());
    }
}
