namespace JuggerHub.Entities;

/// <summary>
/// A user's admin grant over an event. All admins are equal (edit, news, participants,
/// contacts, invite co-admins, cancel); the creator is simply the first admin. An event
/// always keeps at least one admin (last-admin guard).
/// </summary>
public sealed class EventAdmin : BaseEntity
{
    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>When this admin was added (UTC).</summary>
    public DateTime AddedDate { get; set; }

    public Event Event { get; set; } = null!;

    public User User { get; set; } = null!;
}
