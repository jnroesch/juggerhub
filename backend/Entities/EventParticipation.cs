namespace JuggerHub.Entities;

/// <summary>
/// Records that a player (profile) took part in an event with a particular team.
/// The basis for recent activity. Feature 005 adds the real <see cref="TeamId"/> FK
/// (SetNull on team delete) while keeping <see cref="TeamLabel"/> as a display snapshot,
/// so the activity DTO shape is unchanged.
/// </summary>
public sealed class EventParticipation : BaseEntity
{
    public Guid ProfileId { get; set; }

    public Guid EventId { get; set; }

    /// <summary>
    /// Display snapshot of the team name ("with &lt;Team&gt;"). Kept alongside the real
    /// <see cref="TeamId"/> so historical activity still reads after a team is deleted
    /// (former team) — see specs/005-team-space.
    /// </summary>
    public string TeamLabel { get; set; } = string.Empty;

    /// <summary>
    /// The real team this participation was played with (feature 005). Nullable: a
    /// participation may predate teams, and deleting a team sets this null (SetNull) to
    /// preserve the player's activity history while blanking attribution.
    /// </summary>
    public Guid? TeamId { get; set; }

    public PlayerProfile Profile { get; set; } = null!;

    public Event Event { get; set; } = null!;

    public Team? Team { get; set; }
}
