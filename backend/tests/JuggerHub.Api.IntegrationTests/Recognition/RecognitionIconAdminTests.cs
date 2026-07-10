using System.Net;
using System.Net.Http.Headers;

namespace JuggerHub.Api.IntegrationTests.Recognition;

/// <summary>
/// Feature 014 icon removal: after a valid upload the public icon reads 200; removing it makes the
/// public icon 404 (hasIcon becomes false). Remove is idempotent, and 404s for a missing definition.
/// Both catalogues.
/// </summary>
[Collection("Recognition")]
public sealed class RecognitionIconAdminTests
{
    private readonly JuggerHubApiFactory _factory;

    public RecognitionIconAdminTests(JuggerHubApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("badges")]
    [InlineData("achievements")]
    public async Task Remove_icon_clears_it_and_public_read_404s(string resource)
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var anon = _factory.CreateClient();
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, resource);

        var png = new ByteArrayContent(RecognitionTestSupport.TinyPng());
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await admin.PutAsync($"/api/v1/admin/{resource}/{id}/icon", png)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/{resource}/{id}/icon")).StatusCode);

        // Remove → public icon disappears.
        var remove = await admin.DeleteAsync($"/api/v1/admin/{resource}/{id}/icon");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/{resource}/{id}/icon")).StatusCode);

        // Removing again is idempotent (definition exists, no icon).
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/{resource}/{id}/icon")).StatusCode);
    }

    [Theory]
    [InlineData("badges")]
    [InlineData("achievements")]
    public async Task Remove_icon_for_missing_definition_is_404(string resource)
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var resp = await admin.DeleteAsync($"/api/v1/admin/{resource}/{Guid.NewGuid()}/icon");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
