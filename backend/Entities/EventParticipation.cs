namespace JuggerHub.Entities;

/// <summary>
/// Records that a player (profile) took part in an event with a particular team.
/// The basis for recent activity. <see cref="TeamLabel"/> is a lightweight string
/// because no Team model exists yet — it will become a real <c>TeamId</c> FK when a
/// teams feature lands, without changing the activity DTO (research.md §6).
/// </summary>
public sealed class EventParticipation : BaseEntity
{
    public Guid ProfileId { get; set; }

    public Guid EventId { get; set; }

    /// <summary>Display label for the team this participation was played with ("with &lt;Team&gt;").</summary>
    public string TeamLabel { get; set; } = string.Empty;

    public PlayerProfile Profile { get; set; } = null!;

    public Event Event { get; set; } = null!;
}
