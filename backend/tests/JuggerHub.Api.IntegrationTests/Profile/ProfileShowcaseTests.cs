using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace JuggerHub.Api.IntegrationTests.Profile;

/// <summary>
/// A player's showcase gallery (feature 046, GH #99): at most five pictures, ordered, captioned,
/// and gated exactly like the identity avatar.
/// </summary>
/// <remarks>
/// The cap, the gate and the "a refusal changes nothing" rule are all enforced server-side, so most
/// of what matters here cannot be demonstrated by a working screen — these tests are the evidence
/// (constitution Principle I).
/// </remarks>
[Collection("Profile")]
public sealed class ProfileShowcaseTests
{
    private const int MaxImages = 5;

    private readonly JuggerHubApiFactory _factory;

    public ProfileShowcaseTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US1: the owner's gallery ---------------------------------------------

    [Fact]
    public async Task Empty_gallery_lists_nothing_and_an_upload_appears_in_it()
    {
        var (client, handle, _) = await NewPlayerAsync();

        Assert.Empty(await ListAsync(client, handle));

        var created = await AddImageAsync(client);
        Assert.Equal(0, created.GetProperty("position").GetInt32());

        var images = await ListAsync(client, handle);
        var only = Assert.Single(images);
        Assert.Equal(created.GetProperty("id").GetGuid(), only.GetProperty("id").GetGuid());

        // Normalized to WebP by the 034 pipeline, and served through the owner's own address.
        var bytes = await client.GetAsync($"/api/v1/profiles/{handle}/showcase/{only.GetProperty("id").GetGuid()}/image");
        Assert.Equal(HttpStatusCode.OK, bytes.StatusCode);
        Assert.Equal("image/webp", bytes.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Sixth_upload_is_refused_and_leaves_the_five_untouched()
    {
        var (client, handle, _) = await NewPlayerAsync();
        for (var i = 0; i < MaxImages; i++)
        {
            await AddImageAsync(client);
        }

        var before = await ListAsync(client, handle);

        // Straight at the API, bypassing any client-side "gallery full" affordance — the disabled
        // button is UX, the server is the boundary.
        var refused = await PostImageAsync(client);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var after = await ListAsync(client, handle);
        Assert.Equal(MaxImages, after.Count);
        Assert.Equal(
            before.Select(i => i.GetProperty("id").GetGuid()),
            after.Select(i => i.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Ten_simultaneous_uploads_leave_exactly_five()
    {
        // Spec SC-002. This is the test the per-owner FOR UPDATE lock exists for: a plain
        // read-then-insert admits more than five here, and a unique index would admit one.
        var (client, handle, _) = await NewPlayerAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => PostImageAsync(client)));

        var accepted = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var refused = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(MaxImages, accepted);
        Assert.Equal(10 - MaxImages, refused);
        Assert.Equal(MaxImages, (await ListAsync(client, handle)).Count);
    }

    [Fact]
    public async Task Removing_an_image_deletes_its_object_and_closes_the_gap()
    {
        var (client, handle, _) = await NewPlayerAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await AddImageAsync(client)).GetProperty("id").GetGuid());
        }

        var objectKey = await ObjectKeyAsync(ids[1]);
        Assert.True(await Store.ExistsAsync(objectKey));

        var removed = await client.DeleteAsync($"/api/v1/profiles/me/showcase/{ids[1]}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        // The row is gone, the bytes are gone with it (FR-011) — not left for the sweep, which is
        // operator-triggered and therefore no schedule at all.
        Assert.False(await Store.ExistsAsync(objectKey), "the stored object outlived its descriptor");

        var remaining = await ListAsync(client, handle);
        Assert.Equal([ids[0], ids[2]], remaining.Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal([0, 1], remaining.Select(i => i.GetProperty("position").GetInt32()));
    }

    [Fact]
    public async Task Removing_an_already_removed_image_is_not_found_and_changes_nothing()
    {
        var (client, handle, _) = await NewPlayerAsync();
        var id = (await AddImageAsync(client)).GetProperty("id").GetGuid();
        await AddImageAsync(client);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/profiles/me/showcase/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/profiles/me/showcase/{id}")).StatusCode);

        Assert.Single(await ListAsync(client, handle));
    }

    [Fact]
    public async Task Caption_can_be_set_changed_and_cleared()
    {
        var (client, handle, _) = await NewPlayerAsync();
        var id = (await AddImageAsync(client)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await SetCaptionAsync(client, id, "Tempelhofer Feld")).StatusCode);
        Assert.Equal("Tempelhofer Feld", (await ListAsync(client, handle))[0].GetProperty("caption").GetString());

        await SetCaptionAsync(client, id, "First tournament");
        Assert.Equal("First tournament", (await ListAsync(client, handle))[0].GetProperty("caption").GetString());

        // Cleared back to no caption — a complete picture, not an incomplete one (FR-005).
        await SetCaptionAsync(client, id, null);
        Assert.Equal(JsonValueKind.Null, (await ListAsync(client, handle))[0].GetProperty("caption").ValueKind);
    }

    [Fact]
    public async Task Caption_beyond_the_limit_is_refused_and_nothing_is_written()
    {
        var (client, handle, _) = await NewPlayerAsync();
        var id = (await AddImageAsync(client)).GetProperty("id").GetGuid();
        await SetCaptionAsync(client, id, "kept");

        var refused = await SetCaptionAsync(client, id, new string('x', 121));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("kept", (await ListAsync(client, handle))[0].GetProperty("caption").GetString());
    }

    [Fact]
    public async Task Reorder_applies_in_full()
    {
        var (client, handle, _) = await NewPlayerAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await AddImageAsync(client)).GetProperty("id").GetGuid());
        }

        var reversed = ids.AsEnumerable().Reverse().ToList();
        Assert.Equal(HttpStatusCode.NoContent, (await ReorderAsync(client, reversed)).StatusCode);

        var images = await ListAsync(client, handle);
        Assert.Equal(reversed, images.Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal([0, 1, 2], images.Select(i => i.GetProperty("position").GetInt32()));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("duplicate")]
    [InlineData("stranger")]
    public async Task Reorder_that_is_not_a_permutation_is_refused_with_nothing_written(string flavour)
    {
        var (client, handle, _) = await NewPlayerAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await AddImageAsync(client)).GetProperty("id").GetGuid());
        }

        var submitted = flavour switch
        {
            "short" => [ids[0], ids[1]],
            "duplicate" => new List<Guid> { ids[0], ids[0], ids[1] },
            _ => [ids[0], ids[1], Guid.CreateVersion7()],
        };

        var refused = await ReorderAsync(client, submitted);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(ids, (await ListAsync(client, handle)).Select(i => i.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Reorder_referencing_a_removed_image_is_refused_as_stale()
    {
        // The edge case the full-permutation contract exists for: the caller's page still lists a
        // picture that is gone. A "move X to index N" delta could not detect this.
        var (client, handle, _) = await NewPlayerAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await AddImageAsync(client)).GetProperty("id").GetGuid());
        }

        await client.DeleteAsync($"/api/v1/profiles/me/showcase/{ids[2]}");

        Assert.Equal(HttpStatusCode.Conflict, (await ReorderAsync(client, ids)).StatusCode);
        Assert.Equal(2, (await ListAsync(client, handle)).Count);
    }

    [Fact]
    public async Task Showcase_and_avatar_never_affect_each_other()
    {
        // FR-004. The showcase is content; the avatar is identity. Neither replaces the other.
        var (client, handle, _) = await NewPlayerAsync();

        using (var avatar = new MultipartFormDataContent())
        {
            avatar.Add(new ByteArrayContent(Png(64, 64)), "file", "avatar.png");
            (await client.PutAsync("/api/v1/profiles/me/avatar", avatar)).EnsureSuccessStatusCode();
        }

        var id = (await AddImageAsync(client)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/profiles/{handle}/avatar")).StatusCode);

        await client.DeleteAsync($"/api/v1/profiles/me/showcase/{id}");

        // The avatar is still there after the gallery has been emptied.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/profiles/{handle}/avatar")).StatusCode);
        Assert.Empty(await ListAsync(client, handle));
    }

    // --- US5: refusals change nothing -----------------------------------------

    [Theory]
    [InlineData("not-an-image")]
    [InlineData("too-many-pixels")]
    [InlineData("corrupt")]
    [InlineData("empty")]
    public async Task A_refused_upload_leaves_the_gallery_exactly_as_it_was(string flavour)
    {
        var (client, handle, _) = await NewPlayerAsync();
        var kept = new List<Guid>
        {
            (await AddImageAsync(client)).GetProperty("id").GetGuid(),
            (await AddImageAsync(client)).GetProperty("id").GetGuid(),
        };

        var payload = flavour switch
        {
            "not-an-image" => "%PDF-1.7 this is not a picture"u8.ToArray(),
            "too-many-pixels" => HugePng(),
            "corrupt" => Png(64, 64)[..40],
            _ => [],
        };

        var refused = await PostBytesAsync(client, payload);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(kept, (await ListAsync(client, handle)).Select(i => i.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Refusal_reasons_are_distinguishable_and_non_technical()
    {
        var (client, _, _) = await NewPlayerAsync();

        // A BMP is a perfectly readable image of a type the platform does not accept, which is a
        // different situation from bytes that cannot be decoded at all — and a member needs to be
        // told which one happened (FR-016). Bytes that are not an image at all (a renamed PDF) land
        // in the "could not be read" bucket, correctly: that is what happened.
        var unsupportedType = await ReadProblemAsync(await PostBytesAsync(client, Bmp(64, 64)));
        var corrupt = await ReadProblemAsync(await PostBytesAsync(client, Png(64, 64)[..40]));
        var tooManyPixels = await ReadProblemAsync(await PostBytesAsync(client, HugePng()));

        Assert.NotEqual(unsupportedType, corrupt);
        Assert.NotEqual(tooManyPixels, corrupt);
        Assert.NotEqual(unsupportedType, tooManyPixels);

        foreach (var detail in new[] { unsupportedType, corrupt, tooManyPixels })
        {
            Assert.False(string.IsNullOrWhiteSpace(detail));
            Assert.DoesNotContain("Exception", detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("   at ", detail, StringComparison.Ordinal);
        }
    }

    // --- US4: gating -----------------------------------------------------------

    [Fact]
    public async Task Private_profile_is_refused_anonymously_and_served_to_a_signed_in_member()
    {
        var (owner, handle, _) = await NewPlayerAsync();
        var id = (await AddImageAsync(owner)).GetProperty("id").GetGuid();

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/profiles/{handle}/showcase")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await anon.GetAsync($"/api/v1/profiles/{handle}/showcase/{id}/image")).StatusCode);

        var (viewer, _, _) = await NewPlayerAsync();
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/v1/profiles/{handle}/showcase")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await viewer.GetAsync($"/api/v1/profiles/{handle}/showcase/{id}/image")).StatusCode);
    }

    [Fact]
    public async Task Public_profile_is_served_anonymously_and_stops_the_moment_it_goes_private()
    {
        var (owner, handle, _) = await NewPlayerAsync();
        var id = (await AddImageAsync(owner)).GetProperty("id").GetGuid();
        await SetVisibilityAsync(owner, isPublic: true);

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/profiles/{handle}/showcase")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/profiles/{handle}/showcase/{id}/image")).StatusCode);

        // No freshness window: the very next request after going private is refused (FR-021). This is
        // what `Cache-Control: private, no-cache` on the media response buys.
        await SetVisibilityAsync(owner, isPublic: false);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/profiles/{handle}/showcase")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/profiles/{handle}/showcase/{id}/image")).StatusCode);
    }

    [Fact]
    public async Task A_banned_owners_showcase_is_returned_to_nobody()
    {
        var (owner, handle, userId) = await NewPlayerAsync();
        var id = (await AddImageAsync(owner)).GetProperty("id").GetGuid();
        await SetVisibilityAsync(owner, isPublic: true);

        await BanAsync(userId);

        var anon = _factory.CreateClient();
        var (viewer, _, _) = await NewPlayerAsync();

        foreach (var client in new[] { anon, viewer })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/profiles/{handle}/showcase")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/api/v1/profiles/{handle}/showcase/{id}/image")).StatusCode);
        }
    }

    [Fact]
    public async Task Every_refusal_of_an_image_is_the_same_404()
    {
        // FR-023: "no such image", "not permitted" and "nothing there" must be indistinguishable, or
        // the endpoint becomes an oracle for whether a private member has pictures.
        var (owner, handle, _) = await NewPlayerAsync();
        var realId = (await AddImageAsync(owner)).GetProperty("id").GetGuid();

        var anon = _factory.CreateClient();

        var notPermitted = await anon.GetAsync($"/api/v1/profiles/{handle}/showcase/{realId}/image");
        var noSuchImage = await anon.GetAsync($"/api/v1/profiles/{handle}/showcase/{Guid.CreateVersion7()}/image");
        var noSuchHandle = await anon.GetAsync($"/api/v1/profiles/nobodyatall/showcase/{realId}/image");

        Assert.Equal(HttpStatusCode.NotFound, notPermitted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noSuchImage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noSuchHandle.StatusCode);

        // Identical bodies, not merely identical statuses: a difference of a single word would be
        // enough to tell "this member has a picture you may not see" from "there is no such picture".
        // Everything except the per-request traceId, which carries no information about the subject.
        var bodies = await Task.WhenAll(
            DescribeProblemAsync(notPermitted),
            DescribeProblemAsync(noSuchImage),
            DescribeProblemAsync(noSuchHandle));
        Assert.Single(bodies.Distinct());
    }

    [Fact]
    public async Task Another_member_cannot_change_someone_elses_gallery()
    {
        var (owner, handle, _) = await NewPlayerAsync();
        var id = (await AddImageAsync(owner)).GetProperty("id").GetGuid();

        var (stranger, _, _) = await NewPlayerAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/profiles/me/showcase/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetCaptionAsync(stranger, id, "mine now")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ReorderAsync(stranger, [id])).StatusCode);

        var images = await ListAsync(owner, handle);
        Assert.Equal(id, Assert.Single(images).GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, images[0].GetProperty("caption").ValueKind);
    }

    [Fact]
    public async Task No_response_discloses_the_stored_objects_location()
    {
        // FR-022 / SC-004. The object key is the one thing that must never cross the boundary; the
        // ETag is a hash of it precisely so a validator can exist without publishing it.
        var (client, handle, _) = await NewPlayerAsync();
        var id = (await AddImageAsync(client)).GetProperty("id").GetGuid();
        var objectKey = await ObjectKeyAsync(id);

        var listing = await client.GetAsync($"/api/v1/profiles/{handle}/showcase");
        var body = await listing.Content.ReadAsStringAsync();
        var image = await client.GetAsync($"/api/v1/profiles/{handle}/showcase/{id}/image");

        Assert.DoesNotContain(objectKey, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profile-showcase", body, StringComparison.OrdinalIgnoreCase);

        var headers = string.Join('\n', image.Headers.Concat(image.Content.Headers)
            .Select(h => $"{h.Key}: {string.Join(',', h.Value)}"));
        Assert.DoesNotContain(objectKey, headers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profile-showcase", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("private", headers, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_image_whose_object_vanished_degrades_without_taking_the_gallery_with_it()
    {
        // FR-024: a descriptor pointing at bytes that are gone is the ordinary "no picture" outcome,
        // not a 500, and the rest of the gallery still lists and renders.
        var (client, handle, _) = await NewPlayerAsync();
        var first = (await AddImageAsync(client)).GetProperty("id").GetGuid();
        var second = (await AddImageAsync(client)).GetProperty("id").GetGuid();

        await Store.DeleteAsync(await ObjectKeyAsync(first));

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/profiles/{handle}/showcase/{first}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/profiles/{handle}/showcase/{second}/image")).StatusCode);
        Assert.Equal(2, (await ListAsync(client, handle)).Count);
    }

    // --- helpers ---------------------------------------------------------------

    private IMediaStore Store => _factory.Services.GetRequiredService<IMediaStore>();

    private async Task<(HttpClient Client, string Handle, Guid UserId)> NewPlayerAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, handle, userId);
    }

    private static async Task<List<JsonElement>> ListAsync(HttpClient client, string handle)
    {
        var response = await client.GetAsync($"/api/v1/profiles/{handle}/showcase");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<JsonElement>>())!;
    }

    private static Task<HttpResponseMessage> PostImageAsync(HttpClient client) =>
        PostBytesAsync(client, Png(120, 90));

    private static async Task<HttpResponseMessage> PostBytesAsync(HttpClient client, byte[] payload)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(payload), "file", "showcase.png");
        return await client.PostAsync("/api/v1/profiles/me/showcase", content);
    }

    private static async Task<JsonElement> AddImageAsync(HttpClient client)
    {
        var response = await PostImageAsync(client);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    private static Task<HttpResponseMessage> SetCaptionAsync(HttpClient client, Guid id, string? caption) =>
        client.PatchAsJsonAsync($"/api/v1/profiles/me/showcase/{id}", new { caption });

    private static Task<HttpResponseMessage> ReorderAsync(HttpClient client, IReadOnlyList<Guid> ids) =>
        client.PutAsJsonAsync("/api/v1/profiles/me/showcase/order", new { imageIds = ids });

    /// <summary>A response's problem body with the per-request traceId removed, for comparison.</summary>
    private static async Task<string> DescribeProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(body);
        var fields = document.RootElement.EnumerateObject()
            .Where(p => !p.NameEquals("traceId"))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={p.Value}");

        return string.Join('|', fields);
    }

    private static async Task<string?> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
    }

    private static async Task SetVisibilityAsync(HttpClient client, bool isPublic)
    {
        var response = await client.PutAsJsonAsync("/api/v1/profiles/me", new
        {
            displayName = "Showcase Player",
            hometown = (string?)null,
            description = (string?)null,
            pompfen = Array.Empty<string>(),
            isPublic,
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> ObjectKeyAsync(Guid imageId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProfileShowcaseImages
            .IgnoreQueryFilters()
            .Where(g => g.Id == imageId)
            .Select(g => g.ObjectKey)
            .FirstAsync();
    }

    private async Task BanAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Status, JuggerHub.Entities.AccountStatus.Banned)
                .SetProperty(u => u.StatusChangedAt, DateTime.UtcNow));
    }

    /// <summary>A valid image of a type the platform does not accept.</summary>
    private static byte[] Bmp(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(30, 200, 90));
        using var ms = new MemoryStream();
        image.Save(ms, new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder());
        return ms.ToArray();
    }

    private static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(10, 120, 200));
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>A JPEG whose pixel count exceeds the decode guard — rejected from the header alone.</summary>
    private static byte[] HugePng()
    {
        using var image = new Image<Rgba32>(7000, 7000, new Rgba32(200, 40, 40));
        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder { Quality = 30 });
        return ms.ToArray();
    }
}
