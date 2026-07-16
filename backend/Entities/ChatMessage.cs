namespace JuggerHub.Entities;

/// <summary>
/// One entry in a <see cref="Conversation"/> (feature 019) — either a player's message
/// (<see cref="ChatMessageKind.Member"/>) or a quiet system line recording a membership change
/// (<see cref="ChatMessageKind.System"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ordering is the <see cref="BaseEntity.Id"/>, never <see cref="BaseEntity.CreatedDate"/>.</b> The id
/// is a UUIDv7 — timestamp-prefixed and monotonic — so <c>ORDER BY "Id"</c> <em>is</em> chronological
/// order: server-assigned, a total order even for two sends in the same tick, and impossible for a
/// client clock to influence (spec FR-011). It doubles as the read cursor
/// (<see cref="ConversationParticipant.LastReadMessageId"/>) and the keyset paging cursor.
/// </para>
/// <para>
/// <b><see cref="Body"/> is plain text and is never markup.</b> It is stored verbatim and every client
/// binds it as text — a chat is the natural home for stored XSS, and this is the line that closes it
/// (spec FR-014).
/// </para>
/// </remarks>
public sealed class ChatMessage : BaseEntity
{
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Who sent it. <b>Null for a <see cref="ChatMessageKind.System"/> line</b>, which is attributable
    /// to no one — a member can neither forge one nor be blamed for one (spec FR-013).
    /// </summary>
    public Guid? SenderId { get; set; }

    public ChatMessageKind Kind { get; set; } = ChatMessageKind.Member;

    /// <summary>The message text, ≤ 2000 chars. Plain text — never interpreted as markup. Emptied on delete.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// The sender withdrew this message. The row survives to hold its place in the order and render a
    /// neutral tombstone, but <see cref="Body"/> and the link columns are <em>cleared</em> on delete —
    /// the content is genuinely gone from the row, not merely hidden behind a flag that a future query
    /// might forget to check (spec FR-050).
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>What a system line records; set iff <see cref="Kind"/> is <see cref="ChatMessageKind.System"/>.</summary>
    public ChatSystemEvent? SystemEvent { get; set; }

    /// <summary>Who a system line is about ("Nia B. joined the team"); set iff <see cref="Kind"/> is System.</summary>
    public Guid? SystemSubjectUserId { get; set; }

    /// <summary>The kind of JuggerHub item a pasted link referred to, parsed from <see cref="Body"/> at send.</summary>
    public ChatLinkKind LinkKind { get; set; } = ChatLinkKind.None;

    /// <summary>
    /// The linked item's id. Deliberately <b>not</b> a foreign key: a loose reference lets a deleted
    /// target degrade the message to a plain link instead of cascading into the thread (spec FR-041).
    /// Only the id is stored — never a snapshot of the target's fields — which is what allows the card
    /// to be resolved against each <em>viewer's</em> permissions at read time (spec FR-040).
    /// </summary>
    public Guid? LinkTargetId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public User? Sender { get; set; }
}
