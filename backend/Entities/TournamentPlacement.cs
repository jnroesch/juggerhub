namespace JuggerHub.Entities;

/// <summary>
/// One line of a tournament ranking (feature 050): a placement, and either a plain team name or a
/// connection to a JuggerHub <see cref="Team"/>.
/// </summary>
/// <remarks>
/// Invariants, enforced by the result services (the unique index backs the fourth):
/// <list type="number">
/// <item><see cref="TeamId"/>, <see cref="ConnectedByUserId"/> and <see cref="ConnectedAt"/> are
/// either all set or all null.</item>
/// <item>A connection is made only by an admin of the event, to a team with a confirmed
/// (<c>Joined</c>) JuggerHub sign-up for that event; or by a platform admin, to any team — which
/// is how teams that played without a JuggerHub sign-up get connected. Teams never claim.</item>
/// <item>Connecting sets <see cref="Name"/> to the team's name; disconnecting restores
/// <see cref="SourceName"/>.</item>
/// <item>A team holds at most one placement per ranking.</item>
/// </list>
/// <see cref="TugenyTeamId"/> exists only to apply an admin's choices within one import commit and
/// to resolve that import's match sides. It is never read across results: no connection is ever
/// reused, suggested or pre-filled from another placement (owner decision, FR-025).
/// </remarks>
public sealed class TournamentPlacement : BaseEntity
{
    public Guid TournamentResultId { get; set; }

    /// <summary>
    /// The displayed placement, stored normalised to standard competition ranking (1, 2, 3, 3, 5).
    /// </summary>
    public int Position { get; set; }

    /// <summary>Stable order within a tie — the order the rows were submitted in.</summary>
    public int SortIndex { get; set; }

    /// <summary>The name exactly as typed, pasted or imported (e.g. a short name or a typo).</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>
    /// The name shown: the team's name while connected, <see cref="SourceName"/> otherwise, and the
    /// team's last name after the team is deleted (the connection is set null, the snapshot stays).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The connected team. SetNull on team delete — the result stays readable.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Who made the current connection. Restrict, like award grantors.</summary>
    public Guid? ConnectedByUserId { get; set; }

    /// <summary>When the current connection was made (UTC).</summary>
    public DateTime? ConnectedAt { get; set; }

    /// <summary>Tugeny's team id, only on imported rows (see remarks).</summary>
    public int? TugenyTeamId { get; set; }

    public TournamentResult TournamentResult { get; set; } = null!;

    public Team? Team { get; set; }

    public User? ConnectedBy { get; set; }
}
