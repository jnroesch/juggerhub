namespace JuggerHub.Entities;

/// <summary>
/// One player's relationship to one <see cref="Conversation"/> (feature 019). What this row
/// <em>means</em> depends on the conversation's <see cref="ConversationKind"/>:
/// </summary>
/// <remarks>
/// <para>
/// <b>Direct/Group</b> — the row <em>is</em> the membership. Its existence (with a null
/// <see cref="LeftDate"/>) is what grants access.
/// </para>
/// <para>
/// <b>Team/Party</b> — the row is <em>state only</em>. It is created lazily on first access purely so
/// the read marker, mute and hide flags have somewhere to live. It carries no authority: deleting it
/// would not revoke access, and its absence does not deny access. Membership for those kinds is a live
/// roster query (see <c>ChatGuard</c>), which is what makes roster mirroring unmissable (spec FR-025).
/// Never use the presence of this row to decide access.
/// </para>
/// </remarks>
public sealed class ConversationParticipant : BaseEntity
{
    public Guid ConversationId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// How far this player has read: the <see cref="BaseEntity.Id"/> of the newest message they have
    /// seen. Null means they have read nothing.
    /// </summary>
    /// <remarks>
    /// A single marker rather than a receipt row per message per member. Because message ids are
    /// UUIDv7 (timestamp-prefixed, monotonic), "unread" is the keyset predicate
    /// <c>m.Id &gt; LastReadMessageId</c> — a range scan on the primary key. That is what makes unread
    /// counts and read receipts affordable on a 30-player team chat (spec FR-015).
    /// </remarks>
    public Guid? LastReadMessageId { get; set; }

    /// <summary>Muted: still listed in the inbox and still updates its row, but contributes nothing to the nav unread total (spec FR-028).</summary>
    public bool IsMuted { get; set; }

    /// <summary>Hidden: dropped from the inbox listing entirely (spec FR-029).</summary>
    public bool IsHidden { get; set; }

    public DateTime JoinedDate { get; set; }

    /// <summary>
    /// When the player left a <see cref="ConversationKind.Group"/>. The row is kept rather than deleted
    /// so their past messages keep an attributable sender and the thread stays coherent for everyone
    /// else; a row with a non-null LeftDate fails the membership check, so the leaver reads nothing.
    /// Always null for the other kinds — Direct/Team/Party cannot be left (spec FR-026).
    /// </summary>
    public DateTime? LeftDate { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public User User { get; set; } = null!;
}
