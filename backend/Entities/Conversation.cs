namespace JuggerHub.Entities;

/// <summary>
/// A chat thread with a set of participants (feature 019). One of four <see cref="ConversationKind"/>s:
/// a 1:1 <see cref="ConversationKind.Direct"/>, a named <see cref="ConversationKind.Group"/> a player
/// created, or an auto-created <see cref="ConversationKind.Team"/>/<see cref="ConversationKind.Party"/>
/// chat that mirrors a roster.
/// </summary>
/// <remarks>
/// <para>
/// <b>Membership is not uniform across kinds.</b> Direct/Group membership lives in
/// <see cref="ConversationParticipant"/> rows. Team/Party membership is <em>derived</em> from
/// <see cref="TeamMembership"/>/<see cref="PartyMember"/> on every request (see <c>ChatGuard</c>);
/// participant rows exist for those kinds only to hold per-user state (read marker, mute, hide) and
/// carry no authority. That is deliberate: it makes "removed from the team ⇒ loses the chat" true by
/// construction, with no sync step to miss (spec FR-025).
/// </para>
/// <para>
/// Auto chats are created idempotently on first access rather than by a backfill migration — the
/// unique filtered indexes on <see cref="TeamId"/>/<see cref="PartyId"/> make "exactly one chat per
/// team/party" a database guarantee rather than a service race (spec FR-024).
/// </para>
/// </remarks>
public sealed class Conversation : BaseEntity
{
    public ConversationKind Kind { get; set; }

    /// <summary>
    /// The group's name. Required for <see cref="ConversationKind.Group"/>; null for
    /// <see cref="ConversationKind.Direct"/> and for a live Team/Party chat, which derive their display
    /// name at projection time (the other player, the team, the party).
    /// </summary>
    /// <remarks>
    /// One exception: an <b>archived</b> Team/Party chat carries its frozen display name here, because
    /// archival severs the link it used to derive the name from. See <see cref="TeamId"/>.
    /// </remarks>
    public string? Name { get; set; }

    /// <summary>
    /// The mirrored team; set while <see cref="Kind"/> is <see cref="ConversationKind.Team"/> and the
    /// chat is live. <b>Nulled on archival</b> — see the remarks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Team deletion and party disband are <b>hard deletes</b> in this codebase, and
    /// <see cref="TeamMembership"/>/<see cref="PartyMember"/> cascade away with them. Since a live auto
    /// chat <em>derives</em> its membership from that roster, a naive delete would leave an archived
    /// chat that literally nobody can read — the roster it asks "are you a member?" no longer exists.
    /// That would silently break spec FR-027 ("members can still read the history").
    /// </para>
    /// <para>
    /// So archiving is a <b>snapshot</b>, not a flag: <c>ChatConversationService.ArchiveForTeamAsync</c>/
    /// <c>ArchiveForPartyAsync</c> must materialise the derived roster into real
    /// <see cref="ConversationParticipant"/> rows, freeze the display name into <see cref="Name"/>,
    /// null this link, and set <see cref="State"/> to Archived — <em>before</em> the team/party row is
    /// deleted. An archived auto chat is then structurally a read-only group: stored membership, stored
    /// name, no roster link. <see cref="Kind"/> is deliberately left alone so the inbox still tags it
    /// TEAM/PARTY.
    /// </para>
    /// <para>
    /// The foreign keys are <b>Restrict</b> on purpose. If a future delete path forgets to archive
    /// first, it fails loudly at development time instead of silently orphaning an Active conversation
    /// whose membership resolves to nobody. Fail closed, not quiet.
    /// </para>
    /// </remarks>
    public Guid? TeamId { get; set; }

    /// <summary>The mirrored party; set while <see cref="Kind"/> is <see cref="ConversationKind.Party"/> and live. Nulled on archival — see <see cref="TeamId"/>.</summary>
    public Guid? PartyId { get; set; }

    public ConversationState State { get; set; } = ConversationState.Active;

    /// <summary>
    /// Cache of the newest message's timestamp, kept only so the inbox can ORDER BY without a
    /// correlated subquery over <see cref="ChatMessage"/>. It is never authoritative for ordering
    /// <em>within</em> a conversation — that is always the message's UUIDv7 <see cref="BaseEntity.Id"/>.
    /// </summary>
    public DateTime? LastMessageDate { get; set; }

    /// <summary>
    /// For <see cref="ConversationKind.Direct"/> only: the two participants' ids, ordered and joined,
    /// so the pair has one canonical key. A unique filtered index over this column is what makes "at
    /// most one direct conversation per pair" (spec FR-008) a <em>database</em> guarantee — two clients
    /// racing to start the same DM collide on the index instead of interleaving through a
    /// check-then-insert and producing two threads. Null for every other kind.
    /// </summary>
    public string? DirectPairKey { get; set; }

    public Team? Team { get; set; }

    public Party? Party { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    /// <summary>The canonical <see cref="DirectPairKey"/> for a pair of players, order-independent.</summary>
    public static string BuildDirectPairKey(Guid a, Guid b) =>
        a.CompareTo(b) <= 0 ? $"{a:D}:{b:D}" : $"{b:D}:{a:D}";
}
