namespace JuggerHub.Entities;

/// <summary>
/// A temporary subset of one <see cref="Team"/> formed for one teams-only <see cref="Event"/> — a
/// crew that plays for the team but as a smaller group (feature 016). It has no name ("team @
/// event"), is team-members-only, and there is at most one non-disbanded party per (team, event).
/// A party is the <em>pre-entry gathering</em>; the team's actual event entry is the
/// <see cref="EventSignup"/> created when the party is <see cref="PartyStatus.Applied"/>.
/// </summary>
/// <remarks>
/// <see cref="RosterCap"/> is snapshotted from <see cref="Event.RosterCap"/> at formation so later
/// event edits do not retroactively resize existing parties. Disband is a hard delete (members,
/// news, and invitations cascade); there is no stored disbanded state.
/// </remarks>
public sealed class Party : BaseEntity
{
    public Guid TeamId { get; set; }

    public Guid EventId { get; set; }

    /// <summary>Players-per-team cap, snapshotted from the event at formation (≥ 5).</summary>
    public int RosterCap { get; set; }

    /// <summary>Optional message shown to the team on the participation request.</summary>
    public string? Message { get; set; }

    public PartyStatus Status { get; set; } = PartyStatus.Open;

    /// <summary>The team's event entry once <see cref="PartyStatus.Applied"/>; null while open.</summary>
    public Guid? EventSignupId { get; set; }

    /// <summary>The forming team admin (the first party admin).</summary>
    public Guid CreatedByUserId { get; set; }

    public Team Team { get; set; } = null!;

    public Event Event { get; set; } = null!;

    public EventSignup? EventSignup { get; set; }

    public User CreatedBy { get; set; } = null!;

    public ICollection<PartyMember> Members { get; set; } = [];

    public ICollection<PartyNewsPost> News { get; set; } = [];

    public ICollection<PartyAdminInvitation> Invitations { get; set; } = [];
}
