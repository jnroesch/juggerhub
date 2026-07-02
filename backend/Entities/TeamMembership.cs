namespace JuggerHub.Entities;

/// <summary>
/// A user's membership and <see cref="TeamRole"/> within a single team. A user may hold
/// unlimited memberships across teams; a team always keeps at least one admin (last-admin
/// guard). Display data (name, handle, pompfen, avatar) is projected via
/// <see cref="User"/>'s <see cref="PlayerProfile"/>.
/// </summary>
public sealed class TeamMembership : BaseEntity
{
    public Guid TeamId { get; set; }

    public Guid UserId { get; set; }

    public TeamRole Role { get; set; }

    /// <summary>When the user joined the team (UTC).</summary>
    public DateTime JoinedDate { get; set; }

    public Team Team { get; set; } = null!;

    public User User { get; set; } = null!;
}
