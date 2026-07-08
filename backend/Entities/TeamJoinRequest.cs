namespace JuggerHub.Entities;

/// <summary>
/// A player's request to join a team (feature 009) — the inverse of an invitation. A signed-in
/// non-member creates a <see cref="JoinRequestStatus.Pending"/> request; a team admin approves
/// (which creates the <see cref="TeamMembership"/>) or declines. At most one pending request per
/// (team, player) is enforced by a partial unique index.
/// </summary>
public sealed class TeamJoinRequest : BaseEntity
{
    public Guid TeamId { get; set; }

    /// <summary>The player asking to join.</summary>
    public Guid UserId { get; set; }

    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;

    /// <summary>The admin who approved/declined (null while pending).</summary>
    public Guid? DecidedByUserId { get; set; }

    /// <summary>When the request was approved/declined (UTC; null while pending).</summary>
    public DateTime? DecidedDate { get; set; }

    public Team Team { get; set; } = null!;

    public User User { get; set; } = null!;

    public User? DecidedBy { get; set; }
}
