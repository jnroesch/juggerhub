namespace JuggerHub.Entities;

/// <summary>
/// One player's block of another (feature 019). Blocking is the recourse against unwanted contact, and
/// it is load-bearing rather than a nicety: chat's direct-message reach is deliberately open — any
/// signed-in player may start a conversation with any other (spec FR-049) — so this row is the only
/// thing standing between that reach and someone who does not want to be reached.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stored directionally, enforced symmetrically.</b> A row means "Blocker blocked Blocked"; it does
/// not imply the reverse. But the question every call site asks is "may these two hold a direct
/// conversation?", and a block in <em>either</em> direction answers no. That is what stops a blocked
/// player from walking around it by starting a fresh conversation (spec FR-049b).
/// </para>
/// <para>
/// <b>Direct conversations only.</b> A block never touches a group, team or party chat the two players
/// legitimately share — both keep participating normally (spec FR-032).
/// </para>
/// </remarks>
public sealed class UserBlock : BaseEntity
{
    /// <summary>The player who blocked.</summary>
    public Guid BlockerUserId { get; set; }

    /// <summary>The player who was blocked.</summary>
    public Guid BlockedUserId { get; set; }

    public User Blocker { get; set; } = null!;

    public User Blocked { get; set; } = null!;
}
