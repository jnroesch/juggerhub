using System.Net;
using System.Net.Http.Headers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

namespace JuggerHub.Api.IntegrationTests.Recognition;

/// <summary>
/// Feature 014 icon removal: after a valid upload the public icon reads 200; removing it makes the
/// public icon 404 (hasIcon becomes false). Remove is idempotent, and 404s for a missing definition.
/// Plus #101: uploads run through the shared image pipeline — stored as normalized WebP, with
/// undecodable/disallowed input rejected without disturbing an existing icon. Both catalogues.
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

    /// <summary>#101: a large upload is downscaled and re-encoded to WebP before storage.</summary>
    [Theory]
    [InlineData("badges")]
    [InlineData("achievements")]
    public async Task Icon_upload_normalizes_a_large_image_to_a_small_webp(string resource)
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var anon = _factory.CreateClient();
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, resource);

        var png = new ByteArrayContent(RecognitionTestSupport.LargePng(out var sourceLength));
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var upload = await admin.PutAsync($"/api/v1/admin/{resource}/{id}/icon", png);
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);

        var read = await anon.GetAsync($"/api/v1/{resource}/{id}/icon");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("image/webp", read.Content.Headers.ContentType?.MediaType);

        var stored = await read.Content.ReadAsByteArrayAsync();
        Assert.True(stored.Length < sourceLength, "stored WebP should be smaller than the uploaded PNG");

        // Fit profile: the largest side is capped, the (non-square) aspect ratio is preserved.
        using var image = Image.Load(stored);
        Assert.True(Math.Max(image.Width, image.Height) <= 256, "largest side must be within the icon max dimension");
        Assert.NotEqual(image.Width, image.Height);
    }

    /// <summary>#101: undecodable bytes are rejected and the previously stored icon survives.</summary>
    [Theory]
    [InlineData("badges")]
    [InlineData("achievements")]
    public async Task Icon_upload_rejection_leaves_existing_icon_unchanged(string resource)
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var anon = _factory.CreateClient();
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, resource);

        var png = new ByteArrayContent(RecognitionTestSupport.TinyPng());
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await admin.PutAsync($"/api/v1/admin/{resource}/{id}/icon", png)).EnsureSuccessStatusCode();
        var before = await (await anon.GetAsync($"/api/v1/{resource}/{id}/icon")).Content.ReadAsByteArrayAsync();

        // A truncated PNG — real magic bytes, no usable pixel data. The old sniff accepted this.
        var corrupt = new ByteArrayContent(RecognitionTestSupport.LargePng(out _)[..40]);
        corrupt.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var rejected = await admin.PutAsync($"/api/v1/admin/{resource}/{id}/icon", corrupt);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var after = await (await anon.GetAsync($"/api/v1/{resource}/{id}/icon")).Content.ReadAsByteArrayAsync();
        Assert.Equal(before, after);
    }

    /// <summary>#101: a recognized-but-disallowed format (BMP) is rejected — the declared type is never trusted.</summary>
    [Theory]
    [InlineData("badges")]
    [InlineData("achievements")]
    public async Task Icon_upload_rejects_a_disallowed_format(string resource)
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, resource);

        using var img = new Image<Rgba32>(8, 8, new Rgba32(9, 9, 9));
        using var ms = new MemoryStream();
        img.Save(ms, new BmpEncoder());
        var bmp = new ByteArrayContent(ms.ToArray());
        bmp.Headers.ContentType = new MediaTypeHeaderValue("image/png"); // lies about the type

        var resp = await admin.PutAsync($"/api/v1/admin/{resource}/{id}/icon", bmp);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
