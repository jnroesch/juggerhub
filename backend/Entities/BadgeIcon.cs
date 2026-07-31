namespace JuggerHub.Entities;

/// <summary>
/// A badge definition's icon: the descriptor row pointing at the stored image (feature 035 / #97).
/// Kept in a separate table (1:1 optional with <see cref="BadgeDefinition"/>) so catalogue and
/// embedded-display projections never touch it.
/// </summary>
/// <remarks>
/// Bytes live in the media store; this row holds only what is needed to describe and serve a fetch.
/// Unlike <see cref="ProfileAvatar"/>, catalogue icons are intentionally readable by anonymous
/// callers — they are artwork, identical for everyone, and carry no subject data. That difference
/// is a property of the media kind, enforced in the serving endpoint, never in the store: the
/// container is private for every kind alike. See <see cref="ProfileAvatar"/> for why a cascade
/// delete leaves the stored object behind.
/// </remarks>
public sealed class BadgeIcon : BaseEntity
{
    public Guid BadgeDefinitionId { get; set; }

    /// <summary>Content type of the stored object; always image/webp after processing (034/#101).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Location of the bytes in the media store; never disclosed to a client.</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Size of the stored object in bytes.</summary>
    public int SizeBytes { get; set; }

    public BadgeDefinition Definition { get; set; } = null!;
}
