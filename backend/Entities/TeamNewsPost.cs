namespace JuggerHub.Entities;

/// <summary>
/// A team update shown in the read-only News feed. The author's team role is rendered from
/// their current <see cref="TeamMembership"/>. Creating/editing posts (the composer) is a
/// later iteration; this feature only reads the feed (seeded in Development).
/// </summary>
public sealed class TeamNewsPost : BaseEntity
{
    public Guid TeamId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    public Team Team { get; set; } = null!;

    public User Author { get; set; } = null!;
}
