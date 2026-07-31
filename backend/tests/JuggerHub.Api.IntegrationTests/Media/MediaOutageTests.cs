using System.Net;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Services.Media;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace JuggerHub.Api.IntegrationTests.Media;

/// <summary>
/// Behaviour when the media store is unreachable (feature 035 / #97, US5, SC-006).
/// </summary>
/// <remarks>
/// <b>What this covers and what it does not.</b> The store is replaced with one that always throws,
/// so these tests assert how the application <em>degrades</em> — the part we wrote. They do not
/// exercise the Polly pipeline itself; the pipeline's presence is asserted separately in
/// <see cref="MediaStoreTests"/> (transport wiring) and its limits in the resilience option tests.
/// Splitting it this way keeps the outage tests fast and deterministic instead of waiting out real
/// timeouts.
/// </remarks>
public sealed class MediaOutageTests : IClassFixture<JuggerHubApiFactory>
{
    private readonly JuggerHubApiFactory _factory;

    public MediaOutageTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Reading_an_avatar_during_an_outage_degrades_to_no_picture()
    {
        // The page must still render. A picture is not worth a 500 — the frontend already draws a
        // placeholder for "no picture", so that is what an outage should look like.
        var (client, handle) = await MemberWithAvatarAsync();

        using var brokenStore = WithUnavailableStore();
        using var degraded = brokenStore.CreateClient();
        await AuthTestHelpers.LoginAsync(degraded, await EmailForAsync(handle), AuthTestHelpers.ValidPassword);

        var response = await degraded.GetAsync($"/api/v1/profiles/{handle}/avatar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Outage_response_leaks_no_provider_detail()
    {
        // Principle I / FR-031: the client sees a generic outcome; the provider's message, the
        // endpoint, and the account key stay server-side.
        var (_, handle) = await MemberWithAvatarAsync();

        using var brokenStore = WithUnavailableStore();
        using var degraded = brokenStore.CreateClient();

        var response = await degraded.GetAsync($"/api/v1/profiles/{handle}/avatar");
        var body = await response.Content.ReadAsStringAsync();

        foreach (var leak in new[] { "devstoreaccount1", "AccountKey", "blob", "Azure", "Exception", "stack" })
        {
            Assert.DoesNotContain(leak, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Upload_during_an_outage_fails_and_leaves_the_existing_picture_intact()
    {
        // Writes do NOT degrade: an upload that silently did nothing would be worse than one that
        // reports failure. And the 034 guarantee still holds — a failed upload must not disturb
        // what is already stored.
        var (client, handle) = await MemberWithAvatarAsync();
        var before = await (await client.GetAsync($"/api/v1/profiles/{handle}/avatar")).Content.ReadAsByteArrayAsync();

        using var brokenStore = WithUnavailableStore();
        using var degraded = brokenStore.CreateClient();
        await AuthTestHelpers.LoginAsync(degraded, await EmailForAsync(handle), AuthTestHelpers.ValidPassword);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Png()), "file", "avatar.png");
        var upload = await degraded.PutAsync("/api/v1/profiles/me/avatar", content);

        Assert.False(upload.IsSuccessStatusCode, "an upload must not report success when the store is down");

        // Back on the healthy client: the previously stored picture is untouched.
        var after = await (await client.GetAsync($"/api/v1/profiles/{handle}/avatar")).Content.ReadAsByteArrayAsync();
        Assert.Equal(before, after);
    }

    /// <summary>A factory whose media store always fails, standing in for an unreachable store.</summary>
    private WebApplicationFactory<Program> WithUnavailableStore() =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMediaStore>();
            services.AddSingleton<IMediaStore>(new UnavailableMediaStore());
        }));

    private async Task<(HttpClient Client, string Handle)> MemberWithAvatarAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        _emails[handle] = email;

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Png()), "file", "avatar.png");
        (await client.PutAsync("/api/v1/profiles/me/avatar", content)).EnsureSuccessStatusCode();

        return (client, handle);
    }

    private readonly Dictionary<string, string> _emails = [];

    private Task<string> EmailForAsync(string handle) => Task.FromResult(_emails[handle]);

    private static byte[] Png()
    {
        using var image = new Image<Rgba32>(64, 64);
        image.Mutate(ctx => ctx.BackgroundColor(Color.Tomato));
        using var buffer = new MemoryStream();
        image.Save(buffer, new PngEncoder());
        return buffer.ToArray();
    }

    /// <summary>Stands in for a store that cannot be reached at all.</summary>
    private sealed class UnavailableMediaStore : IMediaStore
    {
        private static InvalidOperationException Down() =>
            new("Media store is unreachable (test double).");

        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) =>
            throw Down();

        // Reads degrade rather than throw — this mirrors AzureBlobMediaStore, which catches
        // transport failures on the read path and reports "no picture".
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string key, CancellationToken ct = default) => throw Down();

        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => throw Down();

        public IAsyncEnumerable<MediaObjectInfo> ListAsync(string? prefix = null, CancellationToken ct = default) =>
            throw Down();
    }
}
