namespace JuggerHub.Services.Teams;

/// <summary>
/// Why a poll request was refused (feature 062). Travels to the client as the ProblemDetails
/// <c>code</c> extension (camelCase), so the UI renders its own translated sentence rather than the
/// English <c>detail</c> (GH #179).
/// </summary>
public enum TeamPollCode
{
    /// <summary>The question is empty or longer than <see cref="TeamPollRules.QuestionMaxLength"/>.</summary>
    Question,

    /// <summary>Fewer than <see cref="TeamPollRules.MinOptions"/> or more than <see cref="TeamPollRules.MaxOptions"/> options.</summary>
    OptionCount,

    /// <summary>An option is empty or longer than <see cref="TeamPollRules.OptionMaxLength"/>; carries its index.</summary>
    OptionLength,

    /// <summary>An option repeats an earlier one, ignoring case and surrounding spaces; carries the later one's index.</summary>
    OptionDuplicate,

    /// <summary>The close time is not in the future.</summary>
    ClosesAtPast,

    /// <summary>The close time is more than <see cref="TeamPollRules.MaxCloseAhead"/> ahead.</summary>
    ClosesAtTooFar,

    /// <summary>A one-answer poll given anything but one option, or a several-answer poll given none.</summary>
    ChoiceCount,

    /// <summary>An answer names an option the poll does not have.</summary>
    ChoiceUnknown,

    /// <summary>The team already has <see cref="TeamPollRules.MaxOpenPolls"/> open polls (409).</summary>
    TooManyOpen,

    /// <summary>The poll has closed (409): no answer, change or edit is accepted any more.</summary>
    Closed,

    /// <summary>The poll's content can no longer change: somebody has answered it (409).</summary>
    Answered,
}

/// <summary>A refused request: the code, which option it is about (0-based, option codes only), and English prose for API readers.</summary>
public sealed record TeamPollProblem(TeamPollCode Code, int? OptionIndex, string Reason);

/// <summary>A poll's content as it is stored: trimmed question, trimmed options in order, the close time in UTC.</summary>
public sealed record TeamPollContent(string Question, IReadOnlyList<string> Options, DateTime? ClosesAtUtc);

/// <summary>
/// The rules for what a poll may be (feature 062). Pure — never touches the database — so each rule is
/// unit-tested on its own, the <c>TeamDetailsPolicy</c> precedent. Who may do what, and whether a
/// poll is open, are <c>TeamPollService</c>'s and <c>TeamPollOpen</c>'s.
/// </summary>
/// <remarks>
/// The limits are constants rather than configuration because two of them are column lengths
/// (<c>AppDbContext</c> reads them): a knob raised above its column would turn a validation message
/// into a 500.
/// </remarks>
public static class TeamPollRules
{
    public const int QuestionMaxLength = 200;
    public const int OptionMaxLength = 80;
    public const int MinOptions = 2;
    public const int MaxOptions = 10;

    /// <summary>At most this many open polls per team at once (spec FR-005).</summary>
    public const int MaxOpenPolls = 10;

    /// <summary>How far ahead a close time may lie (spec FR-004).</summary>
    public static readonly TimeSpan MaxCloseAhead = TimeSpan.FromDays(365);

    /// <summary>
    /// Validate and normalise a poll's content, checking in the order a person would fix it: the
    /// question, how many options, each option, duplicates, then the close time.
    /// </summary>
    /// <returns>The content to store, or the first problem found.</returns>
    public static (TeamPollContent? Content, TeamPollProblem? Problem) ValidateContent(
        string? question, IReadOnlyList<string?>? options, DateTimeOffset? closesAt, DateTime utcNow)
    {
        var q = question?.Trim() ?? string.Empty;
        if (q.Length == 0 || q.Length > QuestionMaxLength)
        {
            return Fail(TeamPollCode.Question, null, $"A question needs 1 to {QuestionMaxLength} characters.");
        }

        var raw = options ?? [];
        if (raw.Count < MinOptions || raw.Count > MaxOptions)
        {
            return Fail(TeamPollCode.OptionCount, null, $"A poll needs {MinOptions} to {MaxOptions} options.");
        }

        var trimmed = new List<string>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var text = raw[i]?.Trim() ?? string.Empty;
            if (text.Length == 0 || text.Length > OptionMaxLength)
            {
                return Fail(TeamPollCode.OptionLength, i, $"Each option needs 1 to {OptionMaxLength} characters.");
            }

            trimmed.Add(text);
        }

        // Case-insensitive: "Rot" and " rot " are the same choice to the person answering. The later
        // one is reported, because that is the one the admin just added.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < trimmed.Count; i++)
        {
            if (!seen.Add(trimmed[i]))
            {
                return Fail(TeamPollCode.OptionDuplicate, i, "Two options are the same.");
            }
        }

        DateTime? closesAtUtc = null;
        if (closesAt is { } at)
        {
            var utc = at.UtcDateTime;
            if (utc <= utcNow)
            {
                return Fail(TeamPollCode.ClosesAtPast, null, "The closing time must be in the future.");
            }

            if (utc > utcNow + MaxCloseAhead)
            {
                return Fail(TeamPollCode.ClosesAtTooFar, null, "The closing time can be at most a year ahead.");
            }

            closesAtUtc = utc;
        }

        return (new TeamPollContent(q, trimmed, closesAtUtc), null);
    }

    /// <summary>
    /// Whether a set of chosen options is a valid answer to a poll: exactly one for a one-answer poll,
    /// at least one for a several-answer poll, and every one of them the poll's own.
    /// </summary>
    /// <param name="allowsMultiple">Whether the poll takes several answers.</param>
    /// <param name="chosen">The chosen option ids, already de-duplicated.</param>
    /// <param name="pollOptionIds">The poll's own option ids.</param>
    public static TeamPollProblem? ValidateChoice(
        bool allowsMultiple, IReadOnlyCollection<Guid> chosen, IReadOnlySet<Guid> pollOptionIds)
    {
        if (chosen.Count == 0 || (!allowsMultiple && chosen.Count != 1))
        {
            return new TeamPollProblem(TeamPollCode.ChoiceCount, null,
                allowsMultiple ? "Choose at least one option." : "Choose exactly one option.");
        }

        return chosen.All(pollOptionIds.Contains)
            ? null
            : new TeamPollProblem(TeamPollCode.ChoiceUnknown, null, "That option is not part of this poll.");
    }

    private static (TeamPollContent?, TeamPollProblem?) Fail(TeamPollCode code, int? index, string reason) =>
        (null, new TeamPollProblem(code, index, reason));
}
