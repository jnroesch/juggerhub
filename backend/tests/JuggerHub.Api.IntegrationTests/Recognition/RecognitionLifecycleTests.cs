using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Recognition;

/// <summary>
/// Feature 014 retire ⇄ reinstate lifecycle: retiring blocks new grants but keeps existing awards;
/// reinstating makes the type grantable again. Reinstate on a missing id is 404. Both catalogues.
/// </summary>
[Collection("Recognition")]
public sealed class RecognitionLifecycleTests
{
    private readonly JuggerHubApiFactory _factory;

    public RecognitionLifecycleTests(JuggerHubApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("badges")]
    [InlineData("achievements")]
    public async Task Reinstate_unretires_and_preserves_existing_awards(string resource)
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, holder, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, resource);

        var grant = await admin.PostAsJsonAsync($"/api/v1/admin/{resource}/{id}/awards", new { playerHandle = holder });
        var awardId = (await grant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Retire → new grants refused.
        (await admin.DeleteAsync($"/api/v1/admin/{resource}/{id}")).EnsureSuccessStatusCode();
        var (_, _, other, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var blocked = await admin.PostAsJsonAsync($"/api/v1/admin/{resource}/{id}/awards", new { playerHandle = other });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);

        // Reinstate → grantable again.
        var reinstate = await admin.PostAsJsonAsync($"/api/v1/admin/{resource}/{id}/reinstate", new { });
        Assert.Equal(HttpStatusCode.NoContent, reinstate.StatusCode);
        var allowed = await admin.PostAsJsonAsync($"/api/v1/admin/{resource}/{id}/awards", new { playerHandle = other });
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);

        // The award granted before retirement survived the whole cycle (still revocable).
        var revoke = await admin.DeleteAsync($"/api/v1/admin/{resource}/awards/{awardId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
    }

    [Theory]
    [InlineData("badges")]
    [InlineData("achievements")]
    public async Task Reinstate_missing_definition_is_404(string resource)
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var resp = await admin.PostAsJsonAsync($"/api/v1/admin/{resource}/{Guid.NewGuid()}/reinstate", new { });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
