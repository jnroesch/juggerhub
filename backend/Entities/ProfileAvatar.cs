namespace JuggerHub.Entities;

/// <summary>
/// A profile's avatar: the descriptor row that points at the stored image (feature 035 / #97).
/// Kept in a separate table (1:1 optional with <see cref="PlayerProfile"/>) so profile and list
/// projections never touch it at all.
/// </summary>
/// <remarks>
/// <para>
/// The bytes themselves live in the media store, not here — that move is what makes the bounded
/// showcase galleries of #99 affordable. What stays in Postgres is the small amount of relational
/// data needed to <em>authorize</em> a fetch: which profile owns the image, where it is, and what
/// it is. That is deliberate, not a leftover. The visibility rule (feature 026) and the banned-user
/// query filter are both expressed as EF queries over this row and its navigations; there is no
/// equivalent in blob storage, and no way to answer "does this member have a picture?" across a
/// list page without a relational join.
/// </para>
/// <para>
/// <b>Deleting this row does not delete the object.</b> The relationship cascades in PostgreSQL, so
/// a cascade delete removes this row with no application code running and leaves the stored object
/// unreferenced. That orphan is inert — its key existed only here, never left the backend, and the
/// container is private — but it is reclaimed only by the reconciliation sweep. Application code
/// that deletes media must delete the object explicitly.
/// </para>
/// </remarks>
public sealed class ProfileAvatar : BaseEntity
{
    public Guid ProfileId { get; set; }

    /// <summary>Content type of the stored object; always image/webp after processing (034).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Location of the bytes in the media store. Unguessable by construction and <b>never</b>
    /// disclosed to a client — not in a DTO, not in a header, not as a link.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Size of the stored object in bytes; lets the descriptor describe a fetch without
    /// reaching the media store.</summary>
    public int SizeBytes { get; set; }

    public PlayerProfile Profile { get; set; } = null!;
}
