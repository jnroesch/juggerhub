namespace JuggerHub.Entities;

/// <summary>
/// Shared base for all persisted domain entities (constitution Principle III).
/// </summary>
/// <remarks>
/// The primary key is a UUIDv7 generated app-side via <see cref="Guid.CreateVersion7()"/>.
/// UUIDv7 is timestamp-prefixed, so inserts append to the right edge of the
/// Postgres B-tree like a sequential key — avoiding page splits and WAL
/// amplification — while staying unguessable enough to expose in URLs. The audit
/// timestamps are populated automatically by <c>AuditFieldsInterceptor</c>;
/// services never set them by hand for tracked saves.
/// </remarks>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }
}
