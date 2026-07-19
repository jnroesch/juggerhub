namespace JuggerHub.Entities;

/// <summary>
/// Lifecycle of a <see cref="Party"/>. A party is <see cref="Open"/> while it is being filled and
/// becomes <see cref="Applied"/> once it is listed as the team's entry on the event; withdrawing
/// reverts it to <see cref="Open"/>. There is no "disbanded" state — disband is a hard delete.
/// Serialized as its name (global JsonStringEnumConverter).
/// </summary>
public enum PartyStatus
{
    Open = 0,
    Applied = 1,
}

/// <summary>
/// A stored <see cref="PartyMember"/> answer. Rows exist only for members who are <see cref="In"/>
/// (part of the crew) or have <see cref="Declined"/>; "no response" is derived (a current team
/// member with no row) and is never stored. Serialized as its name.
/// </summary>
public enum PartyMemberStatus
{
    In = 0,
    Declined = 1,
}

/// <summary>A member's role within a party. The creator is the first <see cref="Admin"/>; a party
/// always keeps ≥ 1 admin (last-admin guard). Serialized as its name.</summary>
public enum PartyMemberRole
{
    Member = 0,
    Admin = 1,
}
