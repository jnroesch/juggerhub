using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Recognition;

namespace JuggerHub.Api.IntegrationTests.Badges;

/// <summary>
/// Feature 012 US1 — admin badge catalog + awards against the real API + Postgres container:
/// create/edit/retire, icon upload + public read, grant to player/team, duplicate 409,
/// subject-type mismatch 400, retire preserves awards, revoke + re-grant.
/// </summary>
[Collection("Recognition")]
public sealed class BadgeAdminTests
{
    private readonly JuggerHubApiFactory _factory;

    public BadgeAdminTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_definition_returns_201_with_fields()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);

        var resp = await admin.PostAsJsonAsync("/api/v1/admin/badges", new
        {
            name = "Beta Tester",
            description = "Was here early",
            appliesToPlayers = true,
            appliesToTeams = true,
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Beta Tester", dto.GetProperty("name").GetString());
        Assert.False(dto.GetProperty("isRetired").GetBoolean());
        Assert.False(dto.GetProperty("hasIcon").GetBoolean());
    }

    [Fact]
    public async Task Create_with_no_applicability_is_rejected_400()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);

        var resp = await admin.PostAsJsonAsync("/api/v1/admin/badges", new
        {
            name = "Nowhere",
            description = "applies to nothing",
            appliesToPlayers = false,
            appliesToTeams = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Icon_upload_valid_then_public_read_ok_reject_non_image()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges");

        var png = new ByteArrayContent(RecognitionTestSupport.TinyPng());
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var upload = await admin.PutAsync($"/api/v1/admin/badges/{id}/icon", png);
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);

        // Public read (anonymous) returns the image.
        var anon = _factory.CreateClient();
        var read = await anon.GetAsync($"/api/v1/badges/{id}/icon");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("image/png", read.Content.Headers.ContentType?.MediaType);

        // Non-image bytes are rejected by the magic-byte sniff.
        var junk = new ByteArrayContent("not an image"u8.ToArray());
        junk.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var bad = await admin.PutAsync($"/api/v1/admin/badges/{id}/icon", junk);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Grant_to_player_and_team_succeeds()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (userClient, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var slug = await RecognitionTestSupport.CreateTeamAsync(userClient);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges");

        var toPlayer = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { playerHandle = handle });
        Assert.Equal(HttpStatusCode.Created, toPlayer.StatusCode);
        var award = await toPlayer.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Player", award.GetProperty("subjectType").GetString());
        Assert.Equal("Manual", award.GetProperty("source").GetString());

        var toTeam = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { teamSlug = slug });
        Assert.Equal(HttpStatusCode.Created, toTeam.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_grant_is_409()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges");

        var first = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { playerHandle = handle });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { playerHandle = handle });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Subject_type_mismatch_is_400()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (userClient, _, _, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var slug = await RecognitionTestSupport.CreateTeamAsync(userClient);
        // Players-only badge granted to a team → mismatch.
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges", appliesToPlayers: true, appliesToTeams: false);

        var resp = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { teamSlug = slug });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Grant_with_both_or_neither_subject_is_400()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges");

        var neither = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { });
        Assert.Equal(HttpStatusCode.BadRequest, neither.StatusCode);

        var both = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards",
            new { playerHandle = "someone", teamSlug = "some-team" });
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
    }

    [Fact]
    public async Task Retire_blocks_new_grants_but_keeps_existing_award()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges");

        var grant = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { playerHandle = handle });
        var awardId = (await grant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var retire = await admin.DeleteAsync($"/api/v1/admin/badges/{id}");
        Assert.Equal(HttpStatusCode.NoContent, retire.StatusCode);

        // New grant on a retired badge is refused.
        var (_, _, handle2, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var afterRetire = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { playerHandle = handle2 });
        Assert.Equal(HttpStatusCode.BadRequest, afterRetire.StatusCode);

        // The existing award survived (it is still revocable).
        var revoke = await admin.DeleteAsync($"/api/v1/admin/badges/awards/{awardId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
    }

    [Fact]
    public async Task Revoke_then_regrant_is_allowed()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, "badges");

        var grant = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { playerHandle = handle });
        var awardId = (await grant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var revoke = await admin.DeleteAsync($"/api/v1/admin/badges/awards/{awardId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        // Revoking again is a 404 (no active award).
        var revokeAgain = await admin.DeleteAsync($"/api/v1/admin/badges/awards/{awardId}");
        Assert.Equal(HttpStatusCode.NotFound, revokeAgain.StatusCode);

        // Re-granting after revoke is allowed (new active award).
        var regrant = await admin.PostAsJsonAsync($"/api/v1/admin/badges/{id}/awards", new { playerHandle = handle });
        Assert.Equal(HttpStatusCode.Created, regrant.StatusCode);
    }
}
