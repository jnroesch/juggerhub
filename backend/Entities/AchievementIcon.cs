namespace JuggerHub.Entities;

/// <summary>
/// An achievement definition's icon: the descriptor row pointing at the stored image
/// (feature 035 / #97). Kept in a separate table (1:1 optional with
/// <see cref="AchievementDefinition"/>) so catalogue and display projections never touch it.
/// </summary>
/// <remarks>
/// Same shape and same reasoning as <see cref="BadgeIcon"/>: bytes in the media store, descriptor
/// here, anonymous read allowed for the kind but never by opening the store.
/// </remarks>
public sealed class AchievementIcon : BaseEntity
{
    public Guid AchievementDefinitionId { get; set; }

    /// <summary>Content type of the stored object; always image/webp after processing (034/#101).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Location of the bytes in the media store; never disclosed to a client.</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Size of the stored object in bytes.</summary>
    public int SizeBytes { get; set; }

    public AchievementDefinition Definition { get; set; } = null!;
}
