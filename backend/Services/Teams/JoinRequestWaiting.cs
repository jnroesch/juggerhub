using System.Linq.Expressions;
using JuggerHub.Data;
using JuggerHub.Entities;

namespace JuggerHub.Services.Teams;

/// <summary>
/// The one meaning of a join request that is <b>waiting</b> for an answer (feature 058, spec
/// FR-015): it has been neither answered nor ended, its player is not banned, and its player is not
/// already a member of the team.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four readers, one definition.</b> The team page's queue, Home's <i>Needs you</i>, the
/// <c>Resolved</c> state of the admins' alerts and the statements that answer a request all ask
/// this question. Each deciding it for itself is how two of them come to disagree about the same
/// request — chat's membership predicate drifted exactly that way before it was shared (019).
/// </para>
/// <para>
/// <b>Why "not banned".</b> A ban hides a player everywhere (feature 013), and the queue used to
/// render a banned player's request as a nameless row linking to <c>/u/null</c>. A ban deletes
/// nothing and can be lifted, so the request simply waits again afterwards (FR-021).
/// </para>
/// <para>
/// <b>Why "not already a member".</b> Accepting an invitation ends the player's waiting request
/// (FR-020, <see cref="ITeamJoinRequestService.EndForMemberAsync"/>), but only best-effort, after
/// the membership is committed. This clause is what keeps a request left behind by a failed cleanup
/// from ever asking the admins to decide on someone who is already in.
/// </para>
/// <para>
/// <b>Correlated subqueries, no navigations — keep it that way.</b> The same expression filters the
/// conditional <c>ExecuteUpdate</c> that answers a request. There the request's own conditions must
/// stay in the outer <c>WHERE</c> of the <c>UPDATE</c>: that is what PostgreSQL re-checks against the
/// committed row after waiting for another transaction's row lock, and so what makes a second answer
/// match nothing (research R4). A navigation can move the condition into a join that is not
/// re-checked the same way.
/// </para>
/// </remarks>
internal static class JoinRequestWaiting
{
    public static Expression<Func<TeamJoinRequest, bool>> Predicate(AppDbContext db) =>
        r => r.Status == JoinRequestStatus.Pending
            && !db.Users.Any(u => u.Id == r.UserId && u.Status == AccountStatus.Banned)
            && !db.TeamMemberships.Any(m => m.TeamId == r.TeamId && m.UserId == r.UserId);
}
