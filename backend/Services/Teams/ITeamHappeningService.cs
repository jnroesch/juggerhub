using JuggerHub.Dtos.Teams;

namespace JuggerHub.Services.Teams;

/// <summary>
/// The team-internal "What's happening" feed (feature 044): who joined, what the team was awarded,
/// a training series added, a session cancelled — for <b>members only</b>.
///
/// <para>
/// Distinct from <see cref="ITeamActivityService"/>, which serves the team's <em>event</em> history
/// to any signed-in viewer. The two were deliberately kept separate rather than merged (feature 044
/// decision D5): they answer different questions for different audiences.
/// </para>
///
/// <para>
/// Everything is derived on read — no activity table, no fan-out writes. See
/// <see cref="TeamHappeningDto"/> for why that is load-bearing.
/// </para>
/// </summary>
public interface ITeamHappeningService
{
    /// <summary>
    /// The team's recent internal happenings, newest first, hard-capped and windowed.
    /// </summary>
    /// <returns>
    /// The entries, or <see langword="null"/> when the slug is unknown <em>or</em> the caller is not
    /// a member. The two cases are deliberately indistinguishable — see
    /// <see cref="TeamMembershipGuard"/>: a non-member must not be able to confirm a team exists.
    /// Callers map null to 404.
    /// </returns>
    Task<IReadOnlyList<TeamHappeningDto>?> GetForTeamAsync(string slug, Guid userId, CancellationToken ct = default);
}
