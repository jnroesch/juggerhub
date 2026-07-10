namespace JuggerHub.Entities;

/// <summary>
/// Append-only record of an administrative account action (feature 013, FR-017):
/// who (<see cref="ActorUserId"/>) did what (<see cref="Action"/>) to whom
/// (<see cref="TargetUserId"/>) and when (<see cref="BaseEntity.CreatedDate"/>).
/// Written in the same SaveChanges as the state change it records; never updated
/// or deleted by application code. No read UI exists this pass (spec: records kept,
/// browsing surface comes later). Badge/achievement grant attribution lives on the
/// feature-012 award rows and is deliberately NOT duplicated here.
/// </summary>
public sealed class AdminActionRecord : BaseEntity
{
    /// <summary>The acting platform administrator (FK → AspNetUsers, Restrict).</summary>
    public Guid ActorUserId { get; set; }

    /// <summary>The affected account (FK → AspNetUsers, Restrict).</summary>
    public Guid TargetUserId { get; set; }

    public AdminAccountAction Action { get; set; }

    /// <summary>Reserved for a future "why" note; no UI writes it this pass.</summary>
    public string? Note { get; set; }

    public User Actor { get; set; } = null!;

    public User Target { get; set; } = null!;
}
