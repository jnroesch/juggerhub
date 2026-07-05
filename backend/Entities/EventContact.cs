namespace JuggerHub.Entities;

/// <summary>
/// A free-form point of contact shown publicly on the event page so participants know who to
/// reach for what (e.g. location host, caterer, moderator). At least one of <see cref="Phone"/>
/// or <see cref="Email"/> is required (service-guarded).
/// </summary>
public sealed class EventContact : BaseEntity
{
    public Guid EventId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Free-text role, e.g. "Location host".</summary>
    public string Role { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public Event Event { get; set; } = null!;
}
