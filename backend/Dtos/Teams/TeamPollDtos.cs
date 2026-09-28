using System.ComponentModel.DataAnnotations;

namespace JuggerHub.Dtos.Teams;

// Feature 062 — team polls. See specs/062-team-polls/contracts/team-polls-api.md.

/// <summary>Which of a team's polls to list. Bound from <c>?state=open|closed</c>.</summary>
public enum TeamPollState
{
    Open,
    Closed,
}

/// <summary>
/// A member as a poll names them: a voter under an option, or someone who has not answered yet. Only
/// current, non-banned members are ever named, so both fields are set in practice; they are nullable
/// because the profile they come from is.
/// </summary>
public sealed record TeamPollPersonDto(string? Name, string? Handle);

/// <summary>
/// One option of a poll, as one caller may see it. <see cref="Count"/> and <see cref="Voters"/> are
/// null whenever the caller may not see the result (hidden until they answer, spec FR-012a);
/// <see cref="Voters"/> is <b>always</b> null in an anonymous poll (FR-018).
/// </summary>
public sealed record TeamPollOptionDto(Guid Id, string Text, int? Count, IReadOnlyList<TeamPollPersonDto>? Voters);

/// <summary>
/// A team poll built for ONE caller (feature 062). Every endpoint returns this shape, and it differs by
/// who asks: their own answer, whether the result is visible to them, and — for admins of a named poll
/// only — who has not answered.
/// </summary>
/// <remarks>
/// <b>What must never appear</b>, for any caller including the author and every admin: in an
/// anonymous poll, any other member's identity (no <see cref="TeamPollOptionDto.Voters"/>, no
/// <see cref="NotAnswered"/>); with the result hidden, any count. Built in one place,
/// <c>TeamPollService</c>'s builder, and tested on the raw JSON (research R6).
/// </remarks>
public sealed record TeamPollDto(
    Guid Id,
    string Question,
    bool AllowsMultiple,
    bool IsAnonymous,
    bool ResultsAfterAnswer,
    DateTime CreatedDate,
    DateTime? ClosesAt,
    DateTime? ClosedAt,
    bool IsOpen,
    string? AuthorName,
    string? AuthorHandle,
    int MemberCount,
    int AnsweredCount,
    bool ResultsVisible,
    bool HasAnswers,
    IReadOnlyList<Guid> MyOptionIds,
    IReadOnlyList<TeamPollOptionDto> Options,
    IReadOnlyList<TeamPollPersonDto>? NotAnswered);

/// <summary>Start a poll (admin-only).</summary>
/// <remarks>
/// The attributes are payload guards only, deliberately looser than the rules: a question over 200
/// characters or an eleventh option must reach the service, whose refusal carries a <c>code</c> the
/// client translates, where MVC's own 400 carries none (feature 061's lesson). The rules live in
/// <c>TeamPollRules</c>. Strings are nullable for the same reason: MVC's implicit <c>[Required]</c>
/// refuses a blank string before the service can.
/// </remarks>
public sealed record CreateTeamPollRequest(
    [StringLength(1000)] string? Question,
    [MaxLength(20)] IReadOnlyList<string?>? Options,
    bool AllowsMultiple,
    bool IsAnonymous,
    bool ResultsAfterAnswer,
    // An instant with an offset. The client converts from the viewer's local time; a zone-less value
    // could not be stored as a point in time at all.
    DateTimeOffset? ClosesAt);

/// <summary>
/// Change a poll (admin-only): a full replace of what may change. <b>There is no <c>IsAnonymous</c>
/// here, on purpose</b> — whether a poll is anonymous is fixed when it is started (spec FR-016), and
/// leaving the field out is what makes that impossible to get wrong. The same payload guards as
/// <see cref="CreateTeamPollRequest"/>.
/// </summary>
public sealed record UpdateTeamPollRequest(
    [StringLength(1000)] string? Question,
    [MaxLength(20)] IReadOnlyList<string?>? Options,
    bool AllowsMultiple,
    bool ResultsAfterAnswer,
    DateTimeOffset? ClosesAt);

/// <summary>Give or replace the caller's answer: the chosen options. Duplicates count once.</summary>
public sealed record AnswerTeamPollRequest([MaxLength(20)] IReadOnlyList<Guid>? OptionIds);
