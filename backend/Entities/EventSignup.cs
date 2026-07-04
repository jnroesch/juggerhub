namespace JuggerHub.Entities;

/// <summary>
/// A live registration for an event by a <see cref="User"/> (individuals-only events) OR a
/// <see cref="Team"/> (teams-only events) — exactly one subject is set (DB CHECK). This is the
/// sign-up/waitlist workflow and is <em>distinct</em> from <see cref="EventParticipation"/>
/// (the historical activity/attendance record). Occupied spots = <see cref="SignupStatus.Joined"/>
/// + <see cref="SignupStatus.AwaitingApproval"/>; a <see cref="SignupStatus.Waitlisted"/> entry
/// never counts. Waitlist order is arrival order (<c>CreatedDate</c>); promotion is manual.
/// </summary>
public sealed class EventSignup : BaseEntity
{
    public Guid EventId { get; set; }

    /// <summary>The signed-up user (individuals-only events; null for a team entry).</summary>
    public Guid? UserId { get; set; }

    /// <summary>The entered team (teams-only events; null for an individual entry).</summary>
    public Guid? TeamId { get; set; }

    public SignupStatus Status { get; set; } = SignupStatus.Joined;

    /// <summary>Set when an admin confirms out-of-band payment was received (paid events).</summary>
    public DateTime? PaymentConfirmedDate { get; set; }

    public Event Event { get; set; } = null!;

    public User? User { get; set; }

    public Team? Team { get; set; }
}
