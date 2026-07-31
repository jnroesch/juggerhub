using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using JuggerHub.Common;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Media;

/// <summary>
/// <see cref="IMediaStore"/> over Azure Blob Storage (feature 035 / #97). Azurite serves the same
/// REST API locally and in tests, so this one implementation runs in every environment — there is
/// deliberately no local-only alternative, because a stand-in implementation is free to drift from
/// the thing it stands in for (constitution Principle V).
/// </summary>
/// <remarks>
/// Contains no retry, no backoff, and no timeout of its own. Those come from the shared
/// feature-028 pipeline on the HTTP transport, configured in <c>Program.cs</c>; the Azure SDK's own
/// retry is switched off there so the two never stack. See specs/035 research §3.
/// </remarks>
public sealed class AzureBlobMediaStore : IMediaStore
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobMediaStore> _logger;

    public AzureBlobMediaStore(
        BlobServiceClient serviceClient,
        IOptions<MediaStorageOptions> options,
        ILogger<AzureBlobMediaStore> logger)
    {
        _container = serviceClient.GetBlobContainerClient(options.Value.ContainerName);
        _logger = logger;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(key);

        // Overwrite by design: the key is minted once per upload, so a replayed attempt lands on the
        // same object instead of stranding a second one that nothing references.
        await blob.UploadAsync(
            content,
            new BlobUploadOptions
            {
                // Set on write so the object is self-describing even though we always serve the
                // content type from the descriptor row rather than from the blob.
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            },
            ct);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(key);

        try
        {
            // Download FULLY here rather than handing back a lazy stream.
            //
            // This looks like the less efficient choice and is deliberately not. A lazily-loading
            // stream defers the actual fetch until ASP.NET copies it into the response body — which
            // is *after* the status line and headers have gone out. A store failure at that point
            // cannot be turned into a graceful "no picture": the 200 is already committed, so the
            // request dies mid-body and the caller gets a broken image or a 500. Every failure has
            // to happen while we can still choose the response, and that means before returning.
            //
            // Affordable because media here is small and bounded by the processing pipeline (034):
            // avatars ≤512 KB, icons ≤128 KB, enforced at upload. Repeat views never reach this at
            // all — they revalidate and get a 304. If gallery volume ever makes the buffering cost
            // real, the fix is caching, not going back to a stream that cannot fail safely.
            var content = await blob.DownloadContentAsync(ct);
            return content.Value.Content.ToStream();
        }
        catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
        {
            // A descriptor pointing at an object that is not there is a real, expected state (a
            // failed cutover, a hand-deleted blob, a restored database pointed at the wrong
            // account). It degrades to the ordinary "no picture" outcome rather than an error page,
            // but it is worth an operator's attention because it should not happen on its own.
            _logger.LogWarning(
                "Media object {MediaObjectKey} is referenced by a descriptor but missing from the store.",
                key);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The store is unreachable, timed out, or the circuit breaker is open.
            //
            // READS degrade, they do not fail (FR-029). A picture is not worth a 500: the page must
            // still render, with the placeholder members already see when someone has no picture.
            // Letting this propagate would turn a storage blip into broken pages across the site.
            //
            // Writes deliberately do NOT get this treatment — an upload that silently did nothing
            // would be far worse than one that says "try again".
            //
            // The exception object is passed to the logger but never to the caller: provider
            // messages, endpoints and credentials stay server-side (Principle I, FR-031).
            _logger.LogError(
                ex,
                "Media store unavailable while reading {MediaObjectKey}; serving no picture.",
                key);
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        // DeleteIfExists rather than Delete: reclaiming an object that is already gone is a success,
        // not a fault. The sweep and the replace-avatar path both rely on that being idempotent.
        await _container.GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: ct);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var response = await _container.GetBlobClient(key).ExistsAsync(ct);
        return response.Value;
    }

    public async IAsyncEnumerable<MediaObjectInfo> ListAsync(
        string? prefix = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Streamed, never materialised: the object count is unbounded and Principle III forbids
        // returning an unbounded collection. The SDK pages this behind continuation tokens, so the
        // caller reconciles in batches and memory stays flat regardless of container size.
        var blobs = _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, ct);
        await foreach (var item in blobs)
        {
            yield return new MediaObjectInfo(
                item.Name,
                item.Properties.LastModified ?? DateTimeOffset.MinValue);
        }
    }

    /// <summary>
    /// Create the container if it is absent, so a fresh environment (or a developer's first
    /// <c>docker compose up</c>) works without a manual provisioning step.
    /// </summary>
    /// <remarks>
    /// <b>Never pass a public access level.</b> The overload without one creates a private
    /// container, which is the whole basis of the feature's privacy guarantee — media reaches a
    /// caller only through our own endpoints, after our own authorization check. Terraform sets
    /// <c>allow_nested_items_to_be_public = false</c> at the account level as the backstop, so even
    /// a mistake here cannot open the container in a deployed environment.
    /// </remarks>
    public async Task EnsureContainerAsync(CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
    }
}
