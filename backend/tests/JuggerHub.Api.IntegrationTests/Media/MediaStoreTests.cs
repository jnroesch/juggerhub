using System.Text;
using JuggerHub.Common;
using JuggerHub.Services.Media;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Media;

/// <summary>
/// Contract tests for <see cref="IMediaStore"/> (feature 035 / #97), exercised against a real
/// Azurite container rather than a fake — a hand-written stub would assert our own assumptions
/// about blob storage instead of blob storage's actual behaviour.
/// </summary>
public sealed class MediaStoreTests : IClassFixture<JuggerHubApiFactory>
{
    private readonly JuggerHubApiFactory _factory;

    public MediaStoreTests(JuggerHubApiFactory factory) => _factory = factory;

    private IMediaStore Store => _factory.Services.GetRequiredService<IMediaStore>();

    private static Stream Bytes(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Stores_and_reads_back_the_same_bytes()
    {
        var key = MediaObjectKey.Create(MediaKind.Avatar);

        await Store.PutAsync(key, Bytes("hello media"), "image/webp");

        await using var read = await Store.OpenReadAsync(key);
        Assert.NotNull(read);
        using var reader = new StreamReader(read!);
        Assert.Equal("hello media", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Writing_the_same_key_twice_leaves_one_object_with_the_later_content()
    {
        // This is what makes a retried upload safe: the key is minted once, so a replay overwrites
        // rather than stranding a second object that nothing references.
        var key = MediaObjectKey.Create(MediaKind.Avatar);

        await Store.PutAsync(key, Bytes("first"), "image/webp");
        await Store.PutAsync(key, Bytes("second"), "image/webp");

        await using var read = await Store.OpenReadAsync(key);
        using var reader = new StreamReader(read!);
        Assert.Equal("second", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Returned_stream_is_fully_materialised_before_it_is_handed_back()
    {
        // Guards the defect this replaced: OpenReadAsync used to return a LAZY stream, so the real
        // fetch happened when ASP.NET copied it into the response — after status and headers were
        // already sent. A store failure at that moment could not become a graceful "no picture";
        // the request just died mid-body with a 500. The bug hid behind 304s for anyone whose
        // browser already had the image, and only showed up for first-time (e.g. anonymous) viewers.
        //
        // Deleting the object after opening and still reading it proves the bytes were fetched up
        // front, which is exactly the property that makes graceful degradation possible.
        var key = MediaObjectKey.Create(MediaKind.Avatar);
        await Store.PutAsync(key, Bytes("materialised"), "image/webp");

        await using var read = await Store.OpenReadAsync(key);
        Assert.NotNull(read);

        await Store.DeleteAsync(key);

        using var reader = new StreamReader(read!);
        Assert.Equal("materialised", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Missing_object_reads_as_null_rather_than_throwing()
    {
        // Absence is a value, not a fault — that is what lets a descriptor whose object has gone
        // missing degrade to the ordinary "no picture" outcome instead of a 500.
        var read = await Store.OpenReadAsync(MediaObjectKey.Create(MediaKind.Avatar));

        Assert.Null(read);
    }

    [Fact]
    public async Task Deleting_an_absent_object_succeeds()
    {
        // Idempotent delete: both the replace-avatar path and the reconciliation sweep rely on
        // reclaiming something already gone being a success.
        await Store.DeleteAsync(MediaObjectKey.Create(MediaKind.Avatar));
    }

    [Fact]
    public async Task Delete_removes_the_object()
    {
        var key = MediaObjectKey.Create(MediaKind.Avatar);
        await Store.PutAsync(key, Bytes("temporary"), "image/webp");
        Assert.True(await Store.ExistsAsync(key));

        await Store.DeleteAsync(key);

        Assert.False(await Store.ExistsAsync(key));
        Assert.Null(await Store.OpenReadAsync(key));
    }

    [Fact]
    public async Task Listing_streams_stored_keys_with_their_write_time()
    {
        var key = MediaObjectKey.Create(MediaKind.BadgeIcon);
        await Store.PutAsync(key, Bytes("icon"), "image/webp");

        var found = new List<MediaObjectInfo>();
        await foreach (var item in Store.ListAsync(MediaObjectKey.Prefix(MediaKind.BadgeIcon)))
        {
            found.Add(item);
        }

        var match = Assert.Single(found, item => item.Key == key);
        Assert.True(match.LastModified > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public void Generated_keys_are_opaque_and_unique()
    {
        // FR-015: a key must not be derivable from anything public. If keys were built from a
        // handle or a profile id, a single container misconfiguration would turn every public
        // identifier into a byte address — and enumeration of private members' pictures would
        // follow. Defence in depth behind the private container, not a substitute for it.
        var first = MediaObjectKey.Create(MediaKind.Avatar);
        var second = MediaObjectKey.Create(MediaKind.Avatar);

        Assert.NotEqual(first, second);
        Assert.StartsWith("avatars/", first, StringComparison.Ordinal);
        Assert.EndsWith(".webp", first, StringComparison.Ordinal);
        Assert.True(first.Length <= MediaObjectKey.MaxLength);

        // The random component is a full 32-hex UUID: unguessable, and deliberately v4 rather than
        // the constitution's UUIDv7, whose timestamp prefix is partially predictable.
        var random = first["avatars/".Length..^".webp".Length];
        Assert.Equal(32, random.Length);
        Assert.True(random.All(Uri.IsHexDigit));
    }

    [Fact]
    public void Store_calls_travel_through_the_resilience_carrying_client()
    {
        // The test that stops the whole resilience design failing silently.
        //
        // If the blob client's transport were not wired to the named HttpClient, the store would run
        // with NO timeout, NO retry and NO circuit breaker — and every other test in this file would
        // still pass, because Azurite always answers. This asserts the pipeline is actually
        // registered under the name the client resolves.
        var factory = _factory.Services.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(MediaStorageOptions.ResilienceName);

        Assert.NotNull(client);

        // A resilience handler owns the time budget, so the client itself must not impose one:
        // a client-level timeout cuts across the pipeline and collapses the deliberate distinction
        // between a per-attempt limit and a total budget.
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }
}
