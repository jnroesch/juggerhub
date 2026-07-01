namespace JuggerHub.Entities;

/// <summary>
/// A minimal Jugger event record. Foundation for recent-activity and a later
/// events feature; there is no event-management UI in this feature — events are
/// seeded in local/dev (specs/003-profile/research.md §6).
/// </summary>
public sealed class Event : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The date the event took place (no time component).</summary>
    public DateOnly Date { get; set; }

    public string Location { get; set; } = string.Empty;

    public ICollection<EventParticipation> Participations { get; set; } = [];
}
