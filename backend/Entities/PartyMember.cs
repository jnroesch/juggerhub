namespace JuggerHub.Entities;

/// <summary>
/// A team member's relationship to a <see cref="Party"/> (feature 016). A row exists only for a
/// member who is <see cref="PartyMemberStatus.In"/> or has <see cref="PartyMemberStatus.Declined"/>;
/// "no response" is derived (a current <see cref="TeamMembership"/> with no row). Occupied roster
/// spots = <see cref="PartyMemberStatus.In"/> rows; first-come reopen order is <c>CreatedDate</c>.
/// Removing/leaving deletes the row and never touches the underlying team membership or badges.
/// </summary>
public sealed class PartyMember : BaseEntity
{
    public Guid PartyId { get; set; }

    public Guid UserId { get; set; }

    public PartyMemberStatus Status { get; set; } = PartyMemberStatus.In;

    public PartyMemberRole Role { get; set; } = PartyMemberRole.Member;

    /// <summary>
    /// True for a mercenary seated through the marketplace (feature 017) — a guest who is
    /// <see cref="PartyMemberStatus.In"/> and counts toward the roster cap but is <em>not</em> a member
    /// of the party's team. Renders the "guest · via market" tag; OR-ed into the In-count/roster
    /// predicate so guests are counted and listed alongside team members. Always false for a team
    /// member's row. A guest is never a party admin/co-admin (co-admins stay team-scoped).
    /// </summary>
    public bool ViaMarket { get; set; }

    public Party Party { get; set; } = null!;

    public User User { get; set; } = null!;
}
