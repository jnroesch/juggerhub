namespace JuggerHub.Entities;

/// <summary>
/// A free agent's public post on a teams <see cref="Event"/>'s marketplace board (feature 017) — a
/// "mercenary" advertising themselves to parties short on players. Name and photo are read live from
/// the owner's <see cref="PlayerProfile"/>; the listing itself carries only the positions they play
/// and a short pitch. There is at most <em>one live listing per (user, event)</em>; it is hard-deleted
/// on take-down and automatically when the owner joins any party for that event ("one event, one
/// crew").
/// </summary>
public sealed class MercenaryListing : BaseEntity
{
    public Guid EventId { get; set; }

    /// <summary>The advertising free agent (FK → AspNetUsers).</summary>
    public Guid UserId { get; set; }

    /// <summary>Positions the player offers to play (pompfen/Läufer), stored as a Postgres int[].</summary>
    public List<Pompfe> Positions { get; set; } = [];

    /// <summary>A short free-text pitch (≤ 280 chars, like a profile bio).</summary>
    public string Pitch { get; set; } = string.Empty;

    public Event Event { get; set; } = null!;

    public User User { get; set; } = null!;
}
