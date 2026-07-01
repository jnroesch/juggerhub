namespace JuggerHub.Entities;

/// <summary>
/// A profile's avatar bytes, kept in a separate table (1:1 optional with
/// <see cref="PlayerProfile"/>) so profile/list projections never pull the blob.
/// Stored as Postgres <c>bytea</c> — a deliberate parity-first MVP choice with a
/// documented migration path to object storage (specs/003-profile/research.md §4;
/// backlog TASK-3).
/// </summary>
public sealed class ProfileAvatar : BaseEntity
{
    public Guid ProfileId { get; set; }

    /// <summary>Validated (magic-byte sniffed) image content type: png / jpeg / webp.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Raw image bytes (size-capped on upload).</summary>
    public byte[] Bytes { get; set; } = [];

    public PlayerProfile Profile { get; set; } = null!;
}
