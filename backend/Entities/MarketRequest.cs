namespace JuggerHub.Entities;

/// <summary>
/// The two-way handshake between a recruiting <see cref="Party"/> and a mercenary <see cref="User"/>
/// on a teams event's marketplace (feature 017). A row is a pending request the other side must accept
/// or decline — nobody joins a roster on one side's action alone. <see cref="Direction"/> distinguishes
/// an <see cref="MarketRequestDirection.Application"/> (user → party) from an
/// <see cref="MarketRequestDirection.Invite"/> (party → user, board or direct); it decides who accepts
/// (the recipient) versus who revokes (the initiator, <see cref="CreatedByUserId"/>).
/// </summary>
/// <remarks>
/// At most one <see cref="MarketRequestStatus.Pending"/> row per (party, user) — a filtered unique
/// index backs the service pre-check. Accepting seats the user as a guest <see cref="PartyMember"/>
/// (<c>ViaMarket = true</c>) atomically under the party-row lock, mirroring the feature-016 join.
/// </remarks>
public sealed class MarketRequest : BaseEntity
{
    public Guid PartyId { get; set; }

    /// <summary>The mercenary (applicant or invitee). FK → AspNetUsers.</summary>
    public Guid UserId { get; set; }

    public MarketRequestDirection Direction { get; set; }

    /// <summary>Positions offered (application) or asked for (invite); Postgres int[] of <see cref="Pompfe"/>.</summary>
    public List<Pompfe> Positions { get; set; } = [];

    public MarketRequestStatus Status { get; set; } = MarketRequestStatus.Pending;

    /// <summary>Who created the request — the applicant (application) or the inviting party admin (invite).</summary>
    public Guid CreatedByUserId { get; set; }

    public Party Party { get; set; } = null!;

    public User User { get; set; } = null!;
}
