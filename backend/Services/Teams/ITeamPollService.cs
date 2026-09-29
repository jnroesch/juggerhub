using JuggerHub.Common;
using JuggerHub.Dtos.Teams;

namespace JuggerHub.Services.Teams;

/// <summary>What became of a poll request. The controller maps each to one HTTP answer.</summary>
public enum TeamPollStatus
{
    /// <summary>200 with the poll as the caller now sees it.</summary>
    Ok,

    /// <summary>201 with the new poll.</summary>
    Created,

    /// <summary>400 with a <see cref="TeamPollCode"/>.</summary>
    Invalid,

    /// <summary>409 with a <see cref="TeamPollCode"/>: too many open, closed, or already answered.</summary>
    Conflict,

    /// <summary>403: the caller is a member but not an admin.</summary>
    Forbidden,

    /// <summary>404 "Team not found": no such team, or the caller is not on it — indistinguishable on purpose.</summary>
    TeamNotFound,

    /// <summary>404 "Poll not found": the team exists and the caller is on it, but the poll is not this team's.</summary>
    PollNotFound,
}

/// <summary>The outcome of a poll request: its status, the poll as the caller now sees it, and — when refused — why.</summary>
public sealed record TeamPollResult(
    TeamPollStatus Status,
    TeamPollDto? Poll = null,
    TeamPollCode? Code = null,
    int? OptionIndex = null,
    string? Reason = null)
{
    public static TeamPollResult Of(TeamPollStatus status) => new(status);

    public static TeamPollResult Refused(TeamPollStatus status, TeamPollProblem problem) =>
        new(status, Code: problem.Code, OptionIndex: problem.OptionIndex, Reason: problem.Reason);
}

/// <summary>
/// Team polls (feature 062). Every method resolves the caller's standing on the team first, through
/// <see cref="TeamMembershipGuard"/>: a caller who is not on the team gets exactly what an unknown team
/// gets, so no answer here reveals whether a team has polls (spec FR-032).
/// </summary>
public interface ITeamPollService
{
    /// <summary>
    /// The team's open or closed polls, built for the caller. Member-only; null means the team was not
    /// found or the caller is not on it.
    /// </summary>
    Task<PagedResult<TeamPollDto>?> ListAsync(
        string slug, Guid userId, TeamPollState state, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Start a poll. Admin-only; at most ten open per team. Notifies every other member after it is saved.</summary>
    Task<TeamPollResult> CreateAsync(string slug, Guid userId, CreateTeamPollRequest request, CancellationToken ct = default);

    /// <summary>
    /// Change a poll. Admin-only, open polls only. The question, options and settings change only while
    /// nobody has answered; the close time may move at any point while it is open.
    /// </summary>
    Task<TeamPollResult> UpdateAsync(string slug, Guid pollId, Guid userId, UpdateTeamPollRequest request, CancellationToken ct = default);

    /// <summary>Give or replace the caller's answer. Member-only, open polls only.</summary>
    Task<TeamPollResult> AnswerAsync(string slug, Guid pollId, Guid userId, IReadOnlyList<Guid> optionIds, CancellationToken ct = default);

    /// <summary>Withdraw the caller's answer. Member-only, open polls only; withdrawing nothing is still fine.</summary>
    Task<TeamPollResult> WithdrawAsync(string slug, Guid pollId, Guid userId, CancellationToken ct = default);

    /// <summary>Close an open poll now, for good. Admin-only.</summary>
    Task<TeamPollResult> CloseAsync(string slug, Guid pollId, Guid userId, CancellationToken ct = default);

    /// <summary>Delete a poll, its answers and the alerts that announced it. Admin-only, open or closed.</summary>
    Task<TeamPollStatus> DeleteAsync(string slug, Guid pollId, Guid userId, CancellationToken ct = default);
}
