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

    // --- Marketplace recruiting (feature 017) ---
    // Opt-in, party-admin-controlled visibility on the event's mercenary board. Off by default; the
    // party fills from within the team until an admin flips it on. Real availability is always
    // RosterCap − In-count (SpotsAdvertised is the admin's stated intent, shown on the card).

    /// <summary>Whether the party is publicly listed on the event's mercenary board (default false).</summary>
    public bool IsRecruiting { get; set; }

    /// <summary>The admin's stated "looking for N" (display only; the real gate is RosterCap − In).</summary>
    public int SpotsAdvertised { get; set; }

    /// <summary>Optional short board copy shown to free agents.</summary>
    public string? RecruitBlurb { get; set; }

    /// <summary>Positions the party is recruiting for (pompfen/Läufer); Postgres int[].</summary>
    public List<Pompfe> PositionsNeeded { get; set; } = [];

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

    /// <summary>Marketplace applications/invites for this party (feature 017); cascade-deleted on disband.</summary>
    public ICollection<MarketRequest> MarketRequests { get; set; } = [];
}
