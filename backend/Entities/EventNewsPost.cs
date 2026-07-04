namespace JuggerHub.Entities;

/// <summary>
/// An admin-authored update shown newest-first on the public event page. Read by everyone;
/// posted only by an event admin. Mirrors <see cref="TeamNewsPost"/>.
/// </summary>
public sealed class EventNewsPost : BaseEntity
{
    public Guid EventId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    public Event Event { get; set; } = null!;

    public User Author { get; set; } = null!;
}
