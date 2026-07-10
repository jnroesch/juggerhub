namespace JuggerHub.Entities;

/// <summary>
/// A badge definition's icon bytes, kept in a separate table (1:1 optional with
/// <see cref="BadgeDefinition"/>) so catalog and embedded display projections never pull the
/// blob. Stored as Postgres <c>bytea</c> — the same parity-first MVP approach as
/// <see cref="ProfileAvatar"/> (documented migration path to object storage; GitHub issue #13).
/// </summary>
public sealed class BadgeIcon : BaseEntity
{
    public Guid BadgeDefinitionId { get; set; }

    /// <summary>Validated (magic-byte sniffed) image content type: png / jpeg / webp.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Raw image bytes (size-capped on upload).</summary>
    public byte[] Bytes { get; set; } = [];

    public BadgeDefinition Definition { get; set; } = null!;
}
