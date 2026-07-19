namespace JuggerHub.Entities;

/// <summary>
/// A party update, private to the party's crew (feature 016). Mirrors <see cref="TeamNewsPost"/>
/// but scoped to a <see cref="Party"/>: only <see cref="PartyMemberStatus.In"/> members may read it,
/// only party admins may post, and it is deleted when the party disbands (cascade). Posting notifies
/// the crew (in-app + email), like a team news post.
/// </summary>
public sealed class PartyNewsPost : BaseEntity
{
    public Guid PartyId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    public Party Party { get; set; } = null!;

    public User Author { get; set; } = null!;
}
