namespace JuggerHub.Entities;

/// <summary>
/// A shared <see cref="InvitationKind.Link"/> (reusable by distinct users until expiry/revoke)
/// or a <see cref="InvitationKind.Targeted"/> invite bound to one user (delivered by email) to
/// <em>co-administer</em> an event. Accepting grants an <see cref="EventAdmin"/>. Usable iff
/// <see cref="Status"/> == <see cref="InvitationStatus.Pending"/> and <see cref="ExpiresDate"/>
/// is in the future — "expired" is derived, never stored. Mirrors <see cref="TeamInvitation"/>.
/// </summary>
/// <remarks>
/// <see cref="Token"/> is an opaque, high-entropy capability stored raw so the active link can be
/// re-displayed to admins; exposure is bounded by high entropy + a 7-day expiry + revoke.
/// </remarks>
public sealed class EventAdminInvitation : BaseEntity
{
    public Guid EventId { get; set; }

    public InvitationKind Kind { get; set; }

    /// <summary>Opaque, unguessable, URL-safe token carried in the invite URL.</summary>
    public string Token { get; set; } = string.Empty;

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    /// <summary>Issued time + TTL (default 7 days), in UTC.</summary>
    public DateTime ExpiresDate { get; set; }

    /// <summary>The admin who issued the invitation.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>The invited user (targeted invites only; null for a shared link).</summary>
    public Guid? TargetUserId { get; set; }

    public Event Event { get; set; } = null!;

    public User CreatedBy { get; set; } = null!;

    public User? TargetUser { get; set; }
}
