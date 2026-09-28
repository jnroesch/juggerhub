namespace JuggerHub.Entities;

/// <summary>
/// An update on an event page, shown newest-first to every signed-in player (feature 026 made
/// the page sign-in only). Event admins post it; unlike team and party news, posting notifies
/// nobody. Any current event admin may later edit or delete any post, whoever wrote it
/// (feature 059). Mirrors <see cref="TeamNewsPost"/>.
/// </summary>
public sealed class EventNewsPost : BaseEntity
{
    public Guid EventId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// When a person last changed <see cref="Body"/>; <c>null</c> for a post never edited, which is
    /// every post written before feature 059. Deliberately not derived from
    /// <see cref="BaseEntity.ModifiedDate"/>: that audit field moves on any write to the row, and
    /// "edited" has to mean the text changed. Saving the same text leaves it untouched.
    /// </summary>
    public DateTime? EditedDate { get; set; }

    public Event Event { get; set; } = null!;

    public User Author { get; set; } = null!;
}
