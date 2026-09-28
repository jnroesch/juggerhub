namespace JuggerHub.Entities;

/// <summary>
/// A question a team admin put to their team, with two to ten fixed answers (feature 062). Every
/// current member may answer it, change that answer and withdraw it until it closes, and sees the
/// result; the team's admins manage it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Open or closed is derived, never stored as a state.</b> A poll is open while
/// <see cref="ClosedAt"/> is null and <see cref="ClosesAt"/> is null or still ahead — the single
/// expression <c>TeamPollOpen.At(now)</c>, which every reader uses. Nothing sweeps a poll closed
/// when its time passes: the time itself is the closing.
/// </para>
/// <para>
/// <b>Who counts is derived as well.</b> A <see cref="TeamPollVote"/> counts only while its voter
/// is a current, non-banned member of the team; nothing is rewritten when someone leaves.
/// </para>
/// </remarks>
public sealed class TeamPoll : BaseEntity
{
    public Guid TeamId { get; set; }

    /// <summary>
    /// The admin who started it. The account row is never deleted (feature 037 neutralises it), so
    /// an erased author's poll stays with the team and simply names no one.
    /// </summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>The question, trimmed, 1–200 characters. Plain text: never formatted, never linked.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>One answer (<c>false</c>) or any number of answers (<c>true</c>). Changeable until the first answer.</summary>
    public bool AllowsMultiple { get; set; }

    /// <summary>
    /// Whether answers are anonymous: nobody — not the author, not an admin — is ever shown who chose
    /// what (spec FR-018).
    /// </summary>
    /// <remarks>
    /// <b>Fixed at creation, and no code path updates it</b> (spec FR-016). Members answered under the
    /// promise it makes, so it must not change underneath them. The edit request carries no field for
    /// it, which is what makes this structural rather than a rule someone has to remember.
    /// </remarks>
    public bool IsAnonymous { get; set; }

    /// <summary>
    /// <c>true</c>: a member sees the result only once they have answered, or once the poll has closed
    /// (spec FR-012a) — the author and admins included. <c>false</c>: the result is always visible.
    /// Changeable until the first answer.
    /// </summary>
    public bool ResultsAfterAnswer { get; set; }

    /// <summary>When the poll closes by itself (UTC), or null for no set time. Movable while open.</summary>
    public DateTime? ClosesAt { get; set; }

    /// <summary>
    /// When an admin closed it early (UTC). Set only by closing early and never cleared: a closed poll
    /// cannot be reopened (spec FR-021).
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    public Team Team { get; set; } = null!;

    public User Author { get; set; } = null!;

    /// <summary>The fixed answers, in the admin's order (<see cref="TeamPollOption.Position"/>).</summary>
    public ICollection<TeamPollOption> Options { get; set; } = [];

    public ICollection<TeamPollVote> Votes { get; set; } = [];
}
