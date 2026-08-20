namespace JuggerHub.Services.Media;

/// <summary>
/// Outcome of adding a picture to a showcase gallery (feature 046 / #99). Every failure is a
/// distinct member-facing situation, so the client can say the right thing rather than "invalid
/// image" for all of them (spec FR-016).
/// </summary>
public enum ShowcaseAddStatus
{
    Success,

    /// <summary>No such profile or team.</summary>
    OwnerNotFound,

    /// <summary>The caller may see this gallery but not change it (a team member who is not an admin).</summary>
    Forbidden,

    /// <summary>The owner already holds the maximum number of pictures. Not a processing failure.</summary>
    GalleryFull,

    /// <summary>No bytes were supplied.</summary>
    Empty,

    /// <summary>The bytes are not an accepted image type — judged by content, never by the claimed type.</summary>
    InvalidType,

    /// <summary>The upload exceeds the accepted input size.</summary>
    TooLarge,

    /// <summary>Pixel count exceeds the decode safety limit (feature 034 / #98).</summary>
    TooManyPixels,

    /// <summary>The bytes could not be decoded (corrupt or truncated).</summary>
    Unreadable,

    /// <summary>The media store did not accept the object. Nothing was written.</summary>
    StoreUnavailable,
}

/// <summary>Outcome of changing an existing gallery: caption, removal, or reorder.</summary>
public enum ShowcaseMutateStatus
{
    Success,

    /// <summary>No such image for this owner — deliberately also the answer when it is someone else's.</summary>
    NotFound,

    /// <summary>The caller may see this gallery but not change it.</summary>
    Forbidden,

    /// <summary>The caption exceeds the 120-character limit. Nothing was written.</summary>
    CaptionTooLong,

    /// <summary>
    /// The submitted order is not an exact permutation of the owner's current images — wrong length,
    /// a duplicate, a stranger, or one was removed while the page was open. Nothing was written;
    /// the client should reload.
    /// </summary>
    StaleOrder,
}

/// <summary>The result of an add, carrying what the caller needs to answer with a DTO.</summary>
public readonly record struct ShowcaseAddResult(ShowcaseAddStatus Status, Guid Id, int Position, string? Reason)
{
    public static ShowcaseAddResult Ok(Guid id, int position) => new(ShowcaseAddStatus.Success, id, position, null);

    public static ShowcaseAddResult Fail(ShowcaseAddStatus status, string? reason = null) =>
        new(status, Guid.Empty, 0, reason);
}

/// <summary>
/// The result of a removal. <see cref="ObjectKey"/> is the stored object the caller must now delete —
/// removing the row does not remove the bytes.
/// </summary>
public readonly record struct ShowcaseRemoveResult(ShowcaseMutateStatus Status, string? ObjectKey)
{
    public static ShowcaseRemoveResult Ok(string objectKey) => new(ShowcaseMutateStatus.Success, objectKey);

    public static ShowcaseRemoveResult Fail(ShowcaseMutateStatus status) => new(status, null);
}

/// <summary>
/// The shape both showcase entities share, so one writer can serialize and compact either gallery
/// without knowing which owner it belongs to. Not mapped: EF sees only the concrete entities.
/// </summary>
public interface IShowcaseImage
{
    Guid Id { get; }

    int Position { get; set; }

    string ObjectKey { get; }
}
