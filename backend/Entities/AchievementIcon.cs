namespace JuggerHub.Entities;

/// <summary>
/// An achievement definition's icon bytes, kept in a separate table (1:1 optional with
/// <see cref="AchievementDefinition"/>) so catalog and display projections never pull the blob.
/// Stored as Postgres <c>bytea</c> — same approach as <see cref="BadgeIcon"/> / <see cref="ProfileAvatar"/>.
/// </summary>
public sealed class AchievementIcon : BaseEntity
{
    public Guid AchievementDefinitionId { get; set; }

    /// <summary>Validated (magic-byte sniffed) image content type: png / jpeg / webp.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Raw image bytes (size-capped on upload).</summary>
    public byte[] Bytes { get; set; } = [];

    public AchievementDefinition Definition { get; set; } = null!;
}
