using System.Linq.Expressions;
using JuggerHub.Entities;

namespace JuggerHub.Services.Teams;

/// <summary>
/// What "open" means for a <see cref="TeamPoll"/> (feature 062, research R4) — written once, and used
/// by every reader: the team's list, the answer/withdraw/edit/close guards, the ten-open cap, and
/// Home's <i>Needs you</i>. A second spelling would let the card, Home and the server disagree about
/// whether a poll can still be answered.
/// </summary>
/// <remarks>
/// A poll closes by itself when its time passes because the time <i>is</i> the closing: nothing sweeps
/// it, and an answer arriving a moment later is refused by this same expression.
/// </remarks>
public static class TeamPollOpen
{
    /// <summary>Open: not closed early, and no close time or one still ahead of <paramref name="utcNow"/>.</summary>
    public static Expression<Func<TeamPoll, bool>> At(DateTime utcNow) =>
        p => p.ClosedAt == null && (p.ClosesAt == null || p.ClosesAt > utcNow);

    /// <summary>
    /// Closed: the negation of <see cref="At"/>, built from it rather than spelled out again, so the two
    /// can never drift. EF applies C# null semantics to the negation, so a poll with no close time is
    /// never mistaken for a closed one.
    /// </summary>
    public static Expression<Func<TeamPoll, bool>> ClosedAt(DateTime utcNow)
    {
        var open = At(utcNow);
        return Expression.Lambda<Func<TeamPoll, bool>>(Expression.Not(open.Body), open.Parameters);
    }

    /// <summary>The same rule for a poll already in memory.</summary>
    public static bool IsOpen(DateTime? closedAt, DateTime? closesAt, DateTime utcNow) =>
        closedAt == null && (closesAt == null || closesAt > utcNow);
}
