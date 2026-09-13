using System.Security.Cryptography;
using System.Text;
using JuggerHub.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace JuggerHub.Services.Media;

/// <summary>
/// Shared response shaping for media reads (feature 035 / #97) — one place so avatars and
/// catalogue icons cannot drift apart on the headers that carry security meaning.
/// </summary>
public static class MediaResponse
{
    /// <summary>
    /// Apply caching headers and return the object as a file response, answering <c>304</c> when
    /// the caller already holds the current version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>private</c>, always.</b> A gated avatar must never be storable in a shared cache; a
    /// <c>public</c> directive here would let an intermediary hold and re-serve media to viewers who
    /// were never authorized for it — recreating, one layer out, exactly the exposure that serving
    /// bytes through our own endpoints exists to prevent.
    /// </para>
    /// <para>
    /// <b><c>no-cache</c>, not a freshness window.</b> <c>no-cache</c> permits storing but requires
    /// revalidation. A <c>max-age</c> would stop the browser asking at all, so a member who went
    /// private — or an account that was banned — would keep rendering in someone else's page until
    /// it expired. Revalidation keeps removal effective on the very next request while still
    /// costing only a descriptor read and a 304 when nothing changed.
    /// </para>
    /// <para>
    /// <b>The validator is a hash, never the key.</b> The object key regenerates on every upload,
    /// so a hash of it changes exactly when the bytes change — a correct validator that discloses
    /// nothing. Emitting the raw key would publish the object's location in a response header, which
    /// is precisely what the design forbids.
    /// </para>
    /// </remarks>
    public static IActionResult File(
        ControllerBase controller,
        MediaContent media,
        MediaStorageOptions options) =>
        File(controller, media, options, downloadFileName: null);

    /// <summary>
    /// As <see cref="File(ControllerBase, MediaContent, MediaStorageOptions)"/>, and when
    /// <paramref name="downloadFileName"/> is given, tells the browser to <b>save</b> the bytes
    /// rather than render them (feature 049 / #282).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a security control, not a convenience.</b> Avatars and catalogue icons are
    /// images the platform produced, so rendering them inline is correct. Chat attachments are
    /// files a member supplied, served from the application's own origin — and the application has
    /// <b>no Content-Security-Policy header</b> (a known gap, recorded in feature 033). Anything
    /// that is not a normalized image must therefore arrive as a download, so that a file which
    /// somehow got past the allow-list is saved rather than executed in our origin.
    /// </para>
    /// <para>
    /// The name is written twice, per RFC 6266: a plain <c>filename</c> for old clients, and an
    /// RFC 5987 <c>filename*</c> carrying the real value percent-encoded. <c>ContentDisposition</c>
    /// does that encoding, which is what stops a file called <c>"; evil.exe</c> from injecting a
    /// header — the one place a member-supplied name reaches a response.
    /// </para>
    /// </remarks>
    public static IActionResult File(
        ControllerBase controller,
        MediaContent media,
        MediaStorageOptions options,
        string? downloadFileName)
    {
        var etag = new EntityTagHeaderValue($"\"{Fingerprint(media.ObjectKey)}\"");
        var headers = controller.Response.GetTypedHeaders();

        headers.CacheControl = new CacheControlHeaderValue
        {
            Private = true,
            NoCache = options.CacheRevalidate,
        };
        headers.ETag = etag;

        var requested = controller.Request.GetTypedHeaders().IfNoneMatch;
        if (requested is not null && requested.Any(candidate => candidate.Compare(etag, useStrongComparison: false)))
        {
            // The caller already has this exact object. Dispose the stream we opened — the response
            // carries no body — and answer without transferring anything.
            media.Content.Dispose();
            return controller.StatusCode(StatusCodes.Status304NotModified);
        }

        if (downloadFileName is not null)
        {
            headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileNameStar = downloadFileName,
            };
        }

        return controller.File(media.Content, media.ContentType);
    }

    /// <summary>
    /// A short, stable, non-reversible fingerprint of an object key, safe to publish as an ETag.
    /// </summary>
    private static string Fingerprint(string objectKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(objectKey));
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }
}
