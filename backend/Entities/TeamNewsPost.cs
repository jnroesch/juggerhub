namespace JuggerHub.Entities;

/// <summary>
/// An update in a team's members-only News feed. Admins post it (feature 010), which notifies the
/// rest of the roster; any current admin may later edit or delete any post, whoever wrote it
/// (feature 057). The author's team role is rendered from their current
/// <see cref="TeamMembership"/>.
/// </summary>
public sealed class TeamNewsPost : BaseEntity
{
    public Guid TeamId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// When a person last changed <see cref="Body"/>; <c>null</c> for a post never edited, which is
    /// every post written before feature 057. Deliberately not derived from
    /// <see cref="BaseEntity.ModifiedDate"/>: that audit field moves on any write to the row, and
    /// "edited" has to mean the text changed. Saving the same text leaves it untouched.
    /// </summary>
    public DateTime? EditedDate { get; set; }

    public Team Team { get; set; } = null!;

    public User Author { get; set; } = null!;
}
