namespace JuggerHub.Entities;

/// <summary>
/// A shared <see cref="InvitationKind.Link"/> or a <see cref="InvitationKind.Targeted"/> invite
/// (delivered by email) to <em>co-administer</em> a <see cref="Party"/> (feature 016). Accepting
/// grants the invitee <see cref="PartyMemberRole.Admin"/> on their <see cref="PartyMember"/> row.
/// Restricted to members of the party's team. Usable iff <see cref="Status"/> ==
/// <see cref="InvitationStatus.Pending"/> and <see cref="ExpiresDate"/> is in the future — "expired"
/// is derived, never stored. Mirrors <see cref="EventAdminInvitation"/> field-for-field.
/// </summary>
/// <remarks>
/// <see cref="Token"/> is an opaque, high-entropy capability stored raw so the active link can be
/// re-displayed to admins; exposure is bounded by high entropy + a 7-day expiry + revoke.
/// </remarks>
public sealed class PartyAdminInvitation : BaseEntity
{
    public Guid PartyId { get; set; }

    public InvitationKind Kind { get; set; }

    /// <summary>Opaque, unguessable, URL-safe token carried in the invite URL.</summary>
    public string Token { get; set; } = string.Empty;

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    /// <summary>Issued time + TTL (default 7 days), in UTC.</summary>
    public DateTime ExpiresDate { get; set; }

    /// <summary>The party admin who issued the invitation.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>The invited team member (targeted invites only; null for a shared link).</summary>
    public Guid? TargetUserId { get; set; }

    public Party Party { get; set; } = null!;

    public User CreatedBy { get; set; } = null!;

    public User? TargetUser { get; set; }
}
