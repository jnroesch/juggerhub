namespace JuggerHub.Entities;

/// <summary>
/// The kind of a <see cref="Conversation"/> (feature 019). This is the discriminator that drives
/// everything conditional in chat: where membership comes from, whether the name is stored or derived,
/// whether the conversation can be left or added to, and which tag the inbox row wears. Serialized as
/// its name (global JsonStringEnumConverter), so the API and Angular client speak "Direct"/"Team"
/// rather than opaque integers.
/// </summary>
/// <remarks>
/// The split that matters is <b>manual</b> (<see cref="Direct"/>, <see cref="Group"/>) vs <b>mirrored</b>
/// (<see cref="Team"/>, <see cref="Party"/>). Manual kinds keep their membership in
/// <see cref="ConversationParticipant"/> rows. Mirrored kinds <em>derive</em> membership from the
/// underlying roster on every request — see <c>ChatGuard</c> — so a player removed from a team loses
/// the chat by construction rather than by a sync step that could be missed (spec FR-025).
/// </remarks>
public enum ConversationKind
{
    /// <summary>A 1:1 conversation between exactly two players. At most one per pair (spec FR-008).</summary>
    Direct = 0,

    /// <summary>A named group a player created by picking two or more people. Leavable and addable.</summary>
    Group = 1,

    /// <summary>The auto-created chat for a team; membership mirrors the team roster. Tagged TEAM.</summary>
    Team = 2,

    /// <summary>The auto-created chat for an event party (feature 016); membership mirrors the party roster. Tagged PARTY.</summary>
    Party = 3,

    /// <summary>
    /// A player's "contact the admins" thread for a team (feature 027). A <b>mirrored</b> kind like
    /// <see cref="Team"/>: membership is derived, never stored — the fixed requester
    /// (<see cref="Conversation.RequesterUserId"/>) plus whoever currently holds
    /// <see cref="TeamRole.Admin"/> on the target team (<see cref="Conversation.TeamId"/>). Tagged
    /// ADMINS. At most one per (requester, team). Named "Inquiry" rather than "Contact" to avoid a clash
    /// with the unrelated <see cref="EventContact"/> entity (feature 006).
    /// </summary>
    TeamInquiry = 4,

    /// <summary>
    /// A player's "contact the admins" thread for an event (feature 027). Mirrored like
    /// <see cref="TeamInquiry"/>, but the roster is the event's <see cref="EventAdmin"/> set and the
    /// target is <see cref="Conversation.EventId"/>. Tagged ADMINS. At most one per (requester, event).
    /// </summary>
    EventInquiry = 5,
}

/// <summary>
/// Whether a <see cref="Conversation"/> still accepts writes (feature 019). Archiving is <b>one-way</b>:
/// nothing transitions back to <see cref="Active"/> (spec FR-027).
/// </summary>
public enum ConversationState
{
    /// <summary>Normal. Accepts messages, typing and (for groups) member changes.</summary>
    Active = 0,

    /// <summary>
    /// The underlying party disbanded or team was deleted. Members can still read the history, but no
    /// message can be posted, no typing is signalled and no realtime is emitted.
    /// </summary>
    Archived = 1,
}

/// <summary>Whether a <see cref="ChatMessage"/> was written by a player or by the system (feature 019).</summary>
public enum ChatMessageKind
{
    /// <summary>A message a player sent. Always carries a sender.</summary>
    Member = 0,

    /// <summary>
    /// A quiet system line recording a membership change ("Nia B. joined the team"). Never carries a
    /// sender — see <see cref="ChatMessage.SenderId"/> — so it can neither be forged by a member nor
    /// attributed to one (spec FR-013).
    /// </summary>
    System = 1,
}

/// <summary>What a system line records (feature 019). The <em>client</em> renders the wording.</summary>
public enum ChatSystemEvent
{
    Joined = 0,
    Left = 1,
    Removed = 2,
    GroupCreated = 3,
}

/// <summary>
/// The kind of JuggerHub item a <see cref="ChatMessage"/> links to, when a pasted link was recognised
/// as one of our own routes (feature 019).
/// </summary>
/// <remarks>
/// Unfurl only ever matches <b>JuggerHub's own route shapes</b> and reads our own database — it never
/// fetches a URL. That is what keeps the SSRF surface of a normal unfurl service from existing at all
/// (spec FR-042). The message stores only the kind and the target's id, never a snapshot of the
/// target's fields, so the card can be resolved against each <em>viewer's</em> permissions at read
/// time rather than frozen at the sender's (spec FR-040).
/// </remarks>
public enum ChatLinkKind
{
    /// <summary>No recognised JuggerHub link; the body renders as plain text.</summary>
    None = 0,

    /// <summary>A player profile — <c>/u/{handle}</c>.</summary>
    Player = 1,

    /// <summary>A team — <c>/t/{slug}</c>.</summary>
    Team = 2,

    /// <summary>An event — <c>/events/{id}</c>.</summary>
    Event = 3,

    /// <summary>A training session — <c>/trainings/sessions/{id}</c>.</summary>
    Training = 4,
}
