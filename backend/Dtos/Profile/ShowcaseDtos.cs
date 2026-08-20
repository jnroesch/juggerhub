namespace JuggerHub.Dtos.Profile;

/// <summary>
/// One picture in a showcase gallery, as the client sees it (feature 046 / #99). Shared by the
/// profile and team surfaces — the two galleries are the same thing with different owners.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the entire DTO, and that is deliberate.</b> There is no object key, no storage URL, no
/// size and no content type. The stored object's location must never cross the client boundary in
/// any form (spec FR-022), and the client does not need the rest: it composes the image address from
/// the owner it already has plus <see cref="Id"/>, exactly as it composes an avatar URL from a
/// handle. Every field not sent is a field that cannot leak.
/// </para>
/// </remarks>
/// <param name="Id">Identifies the picture within its owner's gallery, and forms its image address.</param>
/// <param name="Caption">Member-supplied caption, or null. Untrusted text — render as text, never as markup.</param>
/// <param name="Position">Dense 0-based position; the list is already returned in this order.</param>
public sealed record ShowcaseImageDto(Guid Id, string? Caption, int Position);

/// <summary>Set or clear a picture's caption. Null or blank clears it.</summary>
public sealed record UpdateShowcaseCaptionRequest(string? Caption);

/// <summary>
/// The gallery's complete new order — every current image id, exactly once.
/// </summary>
/// <remarks>
/// A full permutation rather than a "move X to index N" delta: a delta cannot tell that the caller's
/// view is stale, which is precisely the case that matters (a co-admin removed a picture while this
/// page was open). Comparing against the owner's current set detects that for free and turns it into
/// one clean refusal the client answers by reloading (spec FR-010).
/// </remarks>
public sealed record ReorderShowcaseRequest(IReadOnlyList<Guid> ImageIds);
