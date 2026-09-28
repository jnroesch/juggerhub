namespace JuggerHub.Entities;

/// <summary>
/// A party update, private to the party's crew (feature 016). Mirrors <see cref="TeamNewsPost"/>
/// but scoped to a <see cref="Party"/>: only <see cref="PartyMemberStatus.In"/> members may read it,
/// only party admins may post, and it is deleted when the party disbands (cascade). Posting notifies
/// the crew (in-app + email), like a team news post. Any current party admin may later edit or
/// delete any post, whoever wrote it (feature 059).
/// </summary>
public sealed class PartyNewsPost : BaseEntity
{
    public Guid PartyId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// When a person last changed <see cref="Body"/>; <c>null</c> for a post never edited, which is
    /// every post written before feature 059. Deliberately not derived from
    /// <see cref="BaseEntity.ModifiedDate"/>: that audit field moves on any write to the row, and
    /// "edited" has to mean the text changed. Saving the same text leaves it untouched.
    /// </summary>
    public DateTime? EditedDate { get; set; }

    public Party Party { get; set; } = null!;

    public User Author { get; set; } = null!;
}
