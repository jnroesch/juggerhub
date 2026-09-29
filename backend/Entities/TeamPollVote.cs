namespace JuggerHub.Entities;

/// <summary>
/// One chosen option of one member's answer to a <see cref="TeamPoll"/> (feature 062). A
/// multi-answer answer is several rows; "the member has answered" means at least one row exists
/// for the poll and the member.
/// </summary>
/// <remarks>
/// <para>
/// <b>"One answer per member" is enforced by <c>TeamPollService</c>, not by an index.</b> Whether a
/// poll allows one option or several lives on the poll, and no index can reference it. Every write
/// here runs under a lock on the poll row, which also serialises answers against edits and closing
/// (research R2/R3).
/// </para>
/// <para>
/// <b>A row counts only while its voter is a current, non-banned member of the team.</b> That is
/// worked out when the poll is read, so nothing here changes when someone leaves — and a member who
/// comes back finds their answer counted again.
/// </para>
/// <para>
/// Account erasure deletes these rows explicitly (feature 037's owned data). The user foreign key is
/// <c>Restrict</c>, but that forces nothing, because the account row itself is never deleted: the
/// erasure test is the only guard.
/// </para>
/// </remarks>
public sealed class TeamPollVote : BaseEntity
{
    /// <summary>
    /// The poll. Redundant with <see cref="Option"/>'s, and kept so that "has this member answered"
    /// and "remove this member's answer" are single-table statements.
    /// </summary>
    public Guid PollId { get; set; }

    public Guid OptionId { get; set; }

    public Guid UserId { get; set; }

    public TeamPoll Poll { get; set; } = null!;

    public TeamPollOption Option { get; set; } = null!;

    public User User { get; set; } = null!;
}
