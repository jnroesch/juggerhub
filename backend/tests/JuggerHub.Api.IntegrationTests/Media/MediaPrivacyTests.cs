using System.Net;
using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace JuggerHub.Api.IntegrationTests.Media;

/// <summary>
/// The privacy proof for feature 035 (#97).
/// </summary>
/// <remarks>
/// Moving avatar bytes out of Postgres removed the coupling that made the feature-026 visibility
/// rule and the banned-account query filter hold automatically — both used to live in the same
/// query that loaded the bytes. These tests assert the gate was genuinely re-established rather
/// than assumed. If any of them fail, the feature is not shippable regardless of what else is
/// green: the failure mode is a private member's photo becoming reachable, which is the exact
/// regression the spec forbids trading away.
/// </remarks>
[Collection("Profile")]
public sealed class MediaPrivacyTests
{
    private readonly JuggerHubApiFactory _factory;

    public MediaPrivacyTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Private_profile_avatar_is_not_served_anonymously()
    {
        var (owner, handle) = await MemberWithAvatarAsync();

        // Profiles are private by default (026). An anonymous caller gets the same 404 as a
        // missing handle — never a 403, so the endpoint is not an existence oracle.
        using var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/v1/profiles/{handle}/avatar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        // A 404 carries a problem-details body, which is fine — what must never come back is image
        // content. Assert on that rather than on an empty body.
        Assert.NotEqual("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Private_profile_avatar_is_served_to_a_signed_in_member()
    {
        var (_, handle) = await MemberWithAvatarAsync();

        // A different, authenticated member: the gate is "the platform decides per request",
        // not "only the owner".
        var (viewer, _, _) = await RegisterVerifyLoginAsync();
        var response = await viewer.GetAsync($"/api/v1/profiles/{handle}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Public_profile_avatar_is_served_anonymously()
    {
        // The counterpart that stops the gate being over-tightened into "authenticated only".
        // Opting a profile public means a signed-out visitor sees the picture.
        var (owner, handle) = await MemberWithAvatarAsync();
        await MakeProfilePublicAsync(owner);

        using var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/v1/profiles/{handle}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Banning_the_owner_hides_the_avatar_on_the_very_next_request()
    {
        var (owner, handle) = await MemberWithAvatarAsync();
        await MakeProfilePublicAsync(owner);

        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/profiles/{handle}/avatar")).StatusCode);

        await BanOwnerAsync(handle);

        // FR-016: no grace window. The very next request is refused — for the owner's own client
        // too, not just for strangers.
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/profiles/{handle}/avatar")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/profiles/{handle}/avatar")).StatusCode);
    }

    [Fact]
    public async Task Media_store_refuses_a_direct_request_for_a_known_object_key()
    {
        // The check that matters most, and the one no other test covers: everything above could
        // pass while the container is quietly world-readable, because they all go through the API.
        // Here we take a real object key straight from the database and ask storage for it,
        // bypassing the platform entirely (FR-012 / SC-010).
        var (_, handle) = await MemberWithAvatarAsync();
        var objectKey = await AvatarObjectKeyAsync(handle);

        using var direct = new HttpClient();
        var response = await direct.GetAsync(
            $"{_factory.MediaBlobEndpoint}/{JuggerHubApiFactory.MediaContainerName}/{objectKey}");

        Assert.False(
            response.IsSuccessStatusCode,
            "the media store served an object to an unauthenticated caller — the container is not private");
    }

    [Fact]
    public async Task Object_keys_cannot_be_derived_from_public_identifiers()
    {
        // FR-015. If keys were built from the handle or the profile id, a single container
        // misconfiguration would turn every public identifier into a byte address, and bulk
        // enumeration of private members' pictures would follow.
        var (_, handle) = await MemberWithAvatarAsync();
        var objectKey = await AvatarObjectKeyAsync(handle);
        var profileId = await ProfileIdAsync(handle);

        Assert.DoesNotContain(handle, objectKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(profileId.ToString(), objectKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(profileId.ToString("n"), objectKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Avatar_response_never_discloses_the_object_location()
    {
        // FR-013: no header, no body, no link may carry the object's location. The ETag is the
        // subtle one — a validator derived from the key is correct and useful, but it must be a
        // hash, or the response publishes exactly what the design keeps inside the backend.
        var (owner, handle) = await MemberWithAvatarAsync();
        var objectKey = await AvatarObjectKeyAsync(handle);

        var response = await owner.GetAsync($"/api/v1/profiles/{handle}/avatar");
        response.EnsureSuccessStatusCode();

        var headers = string.Join("\n", response.Headers.Concat(response.Content.Headers)
            .Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));

        Assert.DoesNotContain(objectKey, headers, StringComparison.OrdinalIgnoreCase);
        // Not even the random component on its own.
        var random = objectKey["avatars/".Length..^".webp".Length];
        Assert.DoesNotContain(random, headers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(JuggerHubApiFactory.MediaContainerName + "/", headers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blob.core.windows.net", headers, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Avatar_is_cached_privately_and_revalidated()
    {
        // `private` keeps a gated avatar out of shared caches. `no-cache` (revalidate) rather than
        // a freshness window is what makes the ban above effective immediately — a max-age would
        // stop the browser asking at all.
        var (owner, handle) = await MemberWithAvatarAsync();

        var response = await owner.GetAsync($"/api/v1/profiles/{handle}/avatar");
        response.EnsureSuccessStatusCode();

        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl!.Private, "media must never be storable in a shared cache");
        Assert.True(cacheControl.NoCache, "media must be revalidated so removal takes effect immediately");
        Assert.False(cacheControl.Public);
        Assert.Null(cacheControl.MaxAge);
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task Repeat_request_with_a_matching_etag_is_answered_304()
    {
        var (owner, handle) = await MemberWithAvatarAsync();

        var first = await owner.GetAsync($"/api/v1/profiles/{handle}/avatar");
        first.EnsureSuccessStatusCode();
        var etag = first.Headers.ETag!;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/profiles/{handle}/avatar");
        request.Headers.IfNoneMatch.Add(etag);
        var second = await owner.SendAsync(request);

        // A repeat view costs a descriptor read and nothing else — no store call, no bytes.
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(0, second.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task Uploading_a_new_avatar_invalidates_the_previous_cache_validator()
    {
        // The validator must change exactly when the bytes do, or a member would upload a new
        // picture and keep seeing the old one.
        var (owner, handle) = await MemberWithAvatarAsync();
        var before = (await owner.GetAsync($"/api/v1/profiles/{handle}/avatar")).Headers.ETag;

        await UploadAvatarAsync(owner);
        var after = (await owner.GetAsync($"/api/v1/profiles/{handle}/avatar")).Headers.ETag;

        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Descriptor_without_a_stored_object_reads_as_no_picture()
    {
        // The "record without object" edge case: a failed cutover, a hand-deleted blob, or a
        // database restored against the wrong account. It must look exactly like having no picture
        // — the frontend already renders a placeholder for that — not like a server error.
        var (owner, handle) = await MemberWithAvatarAsync();
        var objectKey = await AvatarObjectKeyAsync(handle);

        var store = _factory.Services.GetRequiredService<JuggerHub.Services.Media.IMediaStore>();
        await store.DeleteAsync(objectKey);

        var response = await owner.GetAsync($"/api/v1/profiles/{handle}/avatar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- helpers --------------------------------------------------------------

    private async Task<(HttpClient Owner, string Handle)> MemberWithAvatarAsync()
    {
        var (client, handle, _) = await RegisterVerifyLoginAsync();
        await UploadAvatarAsync(client);
        return (client, handle);
    }

    private async Task<(HttpClient Client, string Handle, string Email)> RegisterVerifyLoginAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, handle, email);
    }

    private static async Task UploadAvatarAsync(HttpClient client)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Png()), "file", "avatar.png");
        (await client.PutAsync("/api/v1/profiles/me/avatar", content)).EnsureSuccessStatusCode();
    }

    private static async Task MakeProfilePublicAsync(HttpClient client)
    {
        var response = await client.PutAsJsonAsync("/api/v1/profiles/me", new
        {
            displayName = "Public Player",
            hometown = (string?)null,
            description = (string?)null,
            pompfen = Array.Empty<string>(),
            isPublic = true,
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> AvatarObjectKeyAsync(string handle)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProfileAvatars
            .Where(a => a.Profile.Handle == handle)
            .Select(a => a.ObjectKey)
            .SingleAsync();
    }

    private async Task<Guid> ProfileIdAsync(string handle)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PlayerProfiles.Where(p => p.Handle == handle).Select(p => p.Id).SingleAsync();
    }

    /// <summary>
    /// Ban the profile's owner directly, the way the admin area does — the point is the effect of
    /// the account state on media, not the admin endpoint that sets it.
    /// </summary>
    private async Task BanOwnerAsync(string handle)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // IgnoreQueryFilters: without it the ban filter would hide the very row we are updating on
        // a re-run, and the update would silently no-op.
        var user = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Profile!.Handle == handle)
            .SingleAsync();

        user.Status = AccountStatus.Banned;
        await db.SaveChangesAsync();
    }

    /// <summary>A small but genuine PNG — the processor decodes every upload, so bytes must be real.</summary>
    private static byte[] Png()
    {
        using var image = new Image<Rgba32>(64, 64);
        image.Mutate(ctx => ctx.BackgroundColor(Color.CornflowerBlue));
        using var buffer = new MemoryStream();
        image.Save(buffer, new PngEncoder());
        return buffer.ToArray();
    }
}
