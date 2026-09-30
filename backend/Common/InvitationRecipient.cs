using JuggerHub.Entities;

namespace JuggerHub.Common;

/// <summary>
/// Who may redeem an invitation (GH #401). A shared <see cref="InvitationKind.Link"/> is a bearer
/// credential: whoever holds its token may use it. A <see cref="InvitationKind.Targeted"/>
/// invitation is bound to one account, and holding its token is not enough.
/// </summary>
/// <remarks>
/// <para>
/// One rule for the team, event and party invitation services. They mirror each other field for
/// field, and all three recorded the recipient and then never read it on accept or decline.
/// </para>
/// <para>
/// <b>How to apply it.</b> Check it first — before status and expiry, before the "already a member /
/// already an admin" shortcut, before any write — and answer a refusal exactly as an unknown token
/// is answered, so the response never confirms that the invitation exists for somebody else.
/// </para>
/// </remarks>
public static class InvitationRecipient
{
    /// <summary>
    /// True when <paramref name="callerId"/> may accept or decline the invitation. Anything that is
    /// not a shared link needs a matching target, so a targeted invitation without one — and any
    /// kind added later — is redeemable by nobody until this rule says otherwise.
    /// </summary>
    public static bool Includes(InvitationKind kind, Guid? targetUserId, Guid callerId) =>
        kind == InvitationKind.Link || targetUserId == callerId;
}
