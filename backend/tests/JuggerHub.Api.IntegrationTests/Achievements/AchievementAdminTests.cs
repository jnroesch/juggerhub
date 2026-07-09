using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Recognition;

namespace JuggerHub.Api.IntegrationTests.Achievements;

/// <summary>
/// Feature 012 US1 — admin achievement catalog + awards. Parallel to the badge suite, focused on
/// the achievement-specific accomplishment context plus the shared invariants (duplicate, mismatch,
/// revoke).
/// </summary>
[Collection("Recognition")]
public sealed class AchievementAdminTests
{
    private readonly JuggerHubApiFactory _factory;

    public AchievementAdminTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Grant_with_context_persists_year_and_label()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (userClient, _, _, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var slug = await RecognitionTestSupport.CreateTeamAsync(userClient);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "achievements", appliesToPlayers: false, appliesToTeams: true);

        var grant = await admin.PostAsJsonAsync($"/api/v1/admin/achievements/{id}/awards",
            new { teamSlug = slug, contextYear = 2026, contextLabel = "National Championship" });

        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        var award = await grant.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Team", award.GetProperty("subjectType").GetString());
        Assert.Equal(2026, award.GetProperty("contextYear").GetInt32());
        Assert.Equal("National Championship", award.GetProperty("contextLabel").GetString());
    }

    [Fact]
    public async Task Duplicate_active_grant_is_409()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "achievements");

        var first = await admin.PostAsJsonAsync($"/api/v1/admin/achievements/{id}/awards", new { playerHandle = handle });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var second = await admin.PostAsJsonAsync($"/api/v1/admin/achievements/{id}/awards", new { playerHandle = handle });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Subject_type_mismatch_is_400()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        // Teams-only achievement granted to a player → mismatch.
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "achievements", appliesToPlayers: false, appliesToTeams: true);

        var resp = await admin.PostAsJsonAsync($"/api/v1/admin/achievements/{id}/awards", new { playerHandle = handle });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Revoke_hides_award_and_allows_regrant()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "achievements");

        var grant = await admin.PostAsJsonAsync($"/api/v1/admin/achievements/{id}/awards", new { playerHandle = handle });
        var awardId = (await grant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/achievements/awards/{awardId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/admin/achievements/awards/{awardId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/v1/admin/achievements/{id}/awards", new { playerHandle = handle })).StatusCode);
    }
}
