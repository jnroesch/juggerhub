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
/// <b><see cref="BodyCipher"/> holds the message text encrypted, not the text</b> (feature 047 /
/// #223). It is an AES-256-GCM envelope — <c>[version:1][nonce:12][tag:16][ciphertext:n]</c>, bound
/// to this row's <see cref="BaseEntity.Id"/> as associated data — and the only thing that produces
/// or reads it is <c>IChatMessageCipher</c>. A database copy on its own therefore reveals no
/// message text; reading messages needs the database <em>and</em> the key, which is deliberately
/// stored nowhere near it.
/// </para>
/// <para>
/// <b>The decrypted text is still plain text and is still never markup.</b> Encryption changed
/// where the bytes are readable, not what they mean: every client still binds the decrypted value
/// as text — a chat is the natural home for stored XSS, and this is the line that closes it
/// (019 FR-014).
/// </para>
/// <para>
/// <b>Nothing may match, sort or filter on message text in SQL.</b> Feature 046 removed the last
/// query that did (the inbox's <c>ILIKE</c>); <c>bytea</c> is what makes reintroducing one
/// impossible rather than merely discouraged.
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

    /// <summary>
    /// The message text (≤ 2000 characters) as an encryption envelope — see the remarks above.
    /// </summary>
    /// <remarks>
    /// <b>A zero-length array means the row holds no text</b>, and that is never the encryption of
    /// an empty string: a system line has nothing to say, and a deleted message's content is
    /// genuinely gone. Encrypting <c>""</c> would produce 29 bytes indistinguishable from a short
    /// message, and "the content is gone from the row" would stop being something anyone could
    /// observe. <c>IChatMessageCipher.Protect</c> throws on empty input for exactly this reason.
    /// </remarks>
    public byte[] BodyCipher { get; set; } = [];

    /// <summary>
    /// The sender withdrew this message. The row survives to hold its place in the order and render a
    /// neutral tombstone, but <see cref="BodyCipher"/> and the link columns are <em>cleared</em> on delete —
    /// the content is genuinely gone from the row, not merely hidden behind a flag that a future query
    /// might forget to check (spec FR-050).
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>What a system line records; set iff <see cref="Kind"/> is <see cref="ChatMessageKind.System"/>.</summary>
    public ChatSystemEvent? SystemEvent { get; set; }

    /// <summary>Who a system line is about ("Nia B. joined the team"); set iff <see cref="Kind"/> is System.</summary>
    public Guid? SystemSubjectUserId { get; set; }

    /// <summary>
    /// The kind of JuggerHub item a pasted link referred to, parsed at send from the text the
    /// player typed — <em>before</em> it is encrypted, which is the only moment it is available
    /// (feature 047 FR-012).
    /// </summary>
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
