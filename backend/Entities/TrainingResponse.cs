namespace JuggerHub.Entities;

/// <summary>
/// One person's current answer to one <see cref="TrainingSession"/> (feature 018). Exactly one row per
/// (session, user) — a unique index enforces it — so changing an answer upserts, never duplicates. No
/// cap on responses; no history retained.
/// </summary>
/// <remarks>
/// <see cref="IsGuest"/> marks an outsider on a public session: a signed-in user who is <em>not</em> a
/// member of the owning team. It is computed server-side at RSVP time (never client-supplied); guests
/// count in the headcount, carry the guest tag, are removable by the admin, and are never added to the
/// team. When a session reverts to team-only, guest rows are excluded from reads.
/// </remarks>
public sealed class TrainingResponse : BaseEntity
{
    public Guid TrainingSessionId { get; set; }

    /// <summary>The responder — always a signed-in user (member or outsider-guest).</summary>
    public Guid UserId { get; set; }

    public TrainingRsvp Answer { get; set; }

    /// <summary>true when the responder is not a member of the owning team (an outsider on a public session).</summary>
    public bool IsGuest { get; set; }

    public TrainingSession Session { get; set; } = null!;

    public User User { get; set; } = null!;
}
