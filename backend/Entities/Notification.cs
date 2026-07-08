namespace JuggerHub.Entities;

/// <summary>
/// A single in-app notification addressed to exactly one recipient (feature 010). The reusable
/// spine every producer plugs into: a <see cref="NotificationType"/> discriminator plus a small
/// JSON <see cref="Payload"/> that carries the type-specific data needed to render the row and
/// drive any inline action. Owned strictly by <see cref="RecipientUserId"/> — every read and
/// mutation is scoped to that user server-side (never trust the client).
/// </summary>
/// <remarks>
/// Source rows (team, invite, news post) are referenced by id *inside the payload*, not by a
/// foreign key, so deleting a source degrades the row gracefully (renders non-navigating) instead
/// of cascading or breaking a join. Timestamps come from <see cref="BaseEntity"/> via the audit
/// interceptor; <c>CreatedDate</c> orders the inbox newest-first.
/// </remarks>
public sealed class Notification : BaseEntity
{
    /// <summary>The user this notification belongs to. Cascade-deleted with the account.</summary>
    public Guid RecipientUserId { get; set; }

    public NotificationType Type { get; set; }

    /// <summary>
    /// Type-specific structured data as JSON (stored <c>jsonb</c>), written in camelCase so the
    /// client reads it directly. Render/target hints only — never an authorization input.
    /// </summary>
    public string Payload { get; set; } = "{}";

    public bool IsRead { get; set; }

    /// <summary>When the notification was first marked read (UTC; null while unread).</summary>
    public DateTime? ReadDate { get; set; }

    /// <summary>
    /// Who caused the notification (for "so-and-so did X"); null for system-originated. Kept out of
    /// the payload so the display name survives an actor rename and can be projected on read.
    /// Set null if the actor account is deleted.
    /// </summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>
    /// Optional natural idempotency key (e.g. <c>invite:{invitationId}</c>). A partial unique index
    /// on (recipient, key) suppresses duplicate unread notifications for the same logical event.
    /// </summary>
    public string? DedupeKey { get; set; }

    public User Recipient { get; set; } = null!;

    public User? Actor { get; set; }
}
