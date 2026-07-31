namespace JuggerHub.Services.Media;

/// <summary>
/// Owner-agnostic binary object storage (feature 035 / #97). Knows about objects and keys and
/// nothing else — no profiles, no teams, no badges, no galleries — so profile avatars, catalogue
/// icons, and the showcase galleries of #99 all share one mechanism.
/// </summary>
/// <remarks>
/// <para>
/// <b>This interface never authorizes anything.</b> It has no notion of a viewer, and adding one
/// would be a design error: every visibility and account-standing decision is made by the calling
/// service, against the descriptor row in Postgres, <em>before</em> <see cref="OpenReadAsync"/> is
/// reached. That ordering is what keeps the feature-026 privacy gate welded to the request rather
/// than delegated to a storage system that cannot express it.
/// </para>
/// <para>
/// <b>Absence is a value, not a fault.</b> A missing object comes back as <c>null</c>/<c>false</c>
/// rather than an exception, so a descriptor whose object has vanished degrades to the same "no
/// picture" outcome members already see instead of a 500.
/// </para>
/// <para>
/// <b>Resilience is inherited, never implemented here.</b> Timeouts, retry, and the circuit breaker
/// come from the shared feature-028 pipeline attached to the HTTP transport in <c>Program.cs</c>.
/// Implementations MUST NOT contain a retry loop, a <c>Task.Delay</c> backoff, or their own
/// timeout — that would stack a second resilience strategy on the first, which multiplies attempts
/// exactly when a struggling dependency can least afford it (constitution Principle VII).
/// </para>
/// </remarks>
public interface IMediaStore
{
    /// <summary>
    /// Store <paramref name="content"/> at <paramref name="key"/>, overwriting any existing object.
    /// </summary>
    /// <remarks>
    /// Overwrite-by-key is what makes a retried write safe: the key is generated once per upload,
    /// before the first attempt, so a replay lands on the same object instead of leaving a second
    /// one behind that nothing references.
    /// </remarks>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Read the object, or return <c>null</c> when it does not exist or cannot be reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned stream MUST be fully materialised — every byte already fetched — and reading it
    /// MUST NOT be able to fail for storage reasons. Implementations may not hand back a lazily
    /// loading stream.
    /// </para>
    /// <para>
    /// This is a correctness requirement, not a performance preference. The caller writes the stream
    /// into an HTTP response, so a fetch deferred until copy-time fails *after* the status and
    /// headers are committed — at which point a storage problem can no longer be turned into the
    /// ordinary "no picture" outcome and the request dies mid-body instead. Every failure has to
    /// surface while the caller can still choose its response.
    /// </para>
    /// </remarks>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);

    /// <summary>Remove the object. Idempotent: deleting an absent object succeeds.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Whether an object is present. For reconciliation and diagnostics, never for authorization.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Stream the keys of stored objects, with the time each was last written, optionally limited
    /// to those under <paramref name="prefix"/>.
    /// </summary>
    /// <remarks>
    /// Used only by the reconciliation sweep, never on a request path. Deliberately an
    /// <see cref="IAsyncEnumerable{T}"/>: a container's object count is unbounded, and Principle III
    /// forbids a service method returning an unbounded collection. The provider's listing is
    /// already continuation-token paged, so streaming is both the natural and the compliant shape —
    /// the caller reconciles in batches and never holds the whole key set in memory.
    /// </remarks>
    IAsyncEnumerable<MediaObjectInfo> ListAsync(string? prefix = null, CancellationToken ct = default);
}

/// <summary>
/// A stored object as seen by the reconciliation sweep: where it is, and when it was last written.
/// The timestamp is what lets the sweep leave in-flight uploads alone.
/// </summary>
public readonly record struct MediaObjectInfo(string Key, DateTimeOffset LastModified);

/// <summary>
/// An authorized media object ready to serve (feature 035 / #97).
/// </summary>
/// <param name="Content">
/// Open stream over the stored object; the caller owns it, and the controller's
/// <c>File(stream, …)</c> disposes it. A stream rather than a byte array so bytes are not held per
/// concurrent request.
/// </param>
/// <param name="ContentType">Content type from the descriptor row, not from the stored object.</param>
/// <param name="ObjectKey">
/// Backend-only, used solely to derive a cache validator. It is <b>hashed</b> before reaching any
/// response header and MUST never appear in a DTO, a header, or a link.
/// </param>
public readonly record struct MediaContent(Stream Content, string ContentType, string ObjectKey);
