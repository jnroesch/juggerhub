using JuggerHub.Services.Teams;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Feature 062, research R8: what a poll may be and what a valid answer is. Pure — the rules never
/// touch the database, so neither do these tests.
/// </summary>
public sealed class TeamPollRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static (TeamPollContent? Content, TeamPollProblem? Problem) Validate(
        string? question = "Thursday instead of Tuesday?",
        IReadOnlyList<string?>? options = null,
        DateTimeOffset? closesAt = null) =>
        TeamPollRules.ValidateContent(question, options ?? ["Thursday", "Tuesday"], closesAt, Now);

    // --- The question ------------------------------------------------------------------------------

    [Fact]
    public void A_valid_poll_is_stored_trimmed_in_the_admins_order()
    {
        var (content, problem) = Validate("  Which jersey colour?  ", ["  Black ", "Orange", "Teal"]);

        Assert.Null(problem);
        Assert.Equal("Which jersey colour?", content!.Question);
        Assert.Equal(["Black", "Orange", "Teal"], content.Options);
        Assert.Null(content.ClosesAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void A_question_with_no_words_is_refused(string? question)
    {
        Assert.Equal(TeamPollCode.Question, Validate(question).Problem?.Code);
    }

    [Fact]
    public void The_question_may_be_200_characters_and_no_more()
    {
        Assert.Null(Validate(new string('q', 200)).Problem);
        Assert.Equal(TeamPollCode.Question, Validate(new string('q', 201)).Problem?.Code);
    }

    // --- Options -------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    public void A_poll_needs_two_to_ten_options(int count)
    {
        var options = Enumerable.Range(0, count).Select(i => (string?)$"Option {i}").ToList();
        Assert.Equal(TeamPollCode.OptionCount, Validate(options: options).Problem?.Code);
    }

    [Fact]
    public void No_options_at_all_is_a_count_problem()
    {
        var (_, problem) = TeamPollRules.ValidateContent("Q?", null, null, Now);
        Assert.Equal(TeamPollCode.OptionCount, problem?.Code);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void Two_and_ten_options_are_accepted(int count)
    {
        var options = Enumerable.Range(0, count).Select(i => (string?)$"Option {i}").ToList();
        Assert.Null(Validate(options: options).Problem);
    }

    [Fact]
    public void An_empty_option_is_refused_with_its_position()
    {
        var problem = Validate(options: ["Black", "   ", "Teal"]).Problem;

        Assert.Equal(TeamPollCode.OptionLength, problem?.Code);
        Assert.Equal(1, problem?.OptionIndex);
    }

    [Fact]
    public void An_option_may_be_80_characters_and_no_more()
    {
        Assert.Null(Validate(options: ["a", new string('o', 80)]).Problem);

        var problem = Validate(options: ["a", new string('o', 81)]).Problem;
        Assert.Equal(TeamPollCode.OptionLength, problem?.Code);
        Assert.Equal(1, problem?.OptionIndex);
    }

    [Fact]
    public void Two_options_that_differ_only_in_case_or_spaces_are_duplicates_and_the_later_one_is_named()
    {
        var problem = Validate(options: ["Rot", " rot ", "Blau"]).Problem;

        Assert.Equal(TeamPollCode.OptionDuplicate, problem?.Code);
        Assert.Equal(1, problem?.OptionIndex);
    }

    // --- The close time ----------------------------------------------------------------------------

    [Fact]
    public void A_close_time_that_is_not_in_the_future_is_refused()
    {
        Assert.Equal(TeamPollCode.ClosesAtPast, Validate(closesAt: new DateTimeOffset(Now)).Problem?.Code);
        Assert.Equal(TeamPollCode.ClosesAtPast, Validate(closesAt: new DateTimeOffset(Now.AddMinutes(-1))).Problem?.Code);
    }

    [Fact]
    public void A_close_time_may_be_a_year_ahead_and_no_more()
    {
        Assert.Null(Validate(closesAt: new DateTimeOffset(Now.AddDays(365))).Problem);
        Assert.Equal(TeamPollCode.ClosesAtTooFar, Validate(closesAt: new DateTimeOffset(Now.AddDays(366))).Problem?.Code);
    }

    [Fact]
    public void A_close_time_given_in_a_local_zone_is_stored_as_the_same_instant_in_UTC()
    {
        // 20:00 in Hamburg summer time is 18:00 UTC.
        var hamburg = new DateTimeOffset(2026, 10, 4, 20, 0, 0, TimeSpan.FromHours(2));

        var content = Validate(closesAt: hamburg).Content;

        Assert.Equal(new DateTime(2026, 10, 4, 18, 0, 0, DateTimeKind.Utc), content!.ClosesAtUtc);
        Assert.Equal(DateTimeKind.Utc, content.ClosesAtUtc!.Value.Kind);
    }

    // --- Answers -----------------------------------------------------------------------------------

    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly IReadOnlySet<Guid> PollOptions = new HashSet<Guid> { A, B };

    [Fact]
    public void A_one_answer_poll_takes_exactly_one_option()
    {
        Assert.Null(TeamPollRules.ValidateChoice(false, [A], PollOptions));
        Assert.Equal(TeamPollCode.ChoiceCount, TeamPollRules.ValidateChoice(false, [A, B], PollOptions)?.Code);
        Assert.Equal(TeamPollCode.ChoiceCount, TeamPollRules.ValidateChoice(false, [], PollOptions)?.Code);
    }

    [Fact]
    public void A_several_answer_poll_takes_one_or_more_options()
    {
        Assert.Null(TeamPollRules.ValidateChoice(true, [A], PollOptions));
        Assert.Null(TeamPollRules.ValidateChoice(true, [A, B], PollOptions));
        Assert.Equal(TeamPollCode.ChoiceCount, TeamPollRules.ValidateChoice(true, [], PollOptions)?.Code);
    }

    [Fact]
    public void An_option_from_another_poll_is_refused()
    {
        Assert.Equal(TeamPollCode.ChoiceUnknown, TeamPollRules.ValidateChoice(true, [A, Guid.NewGuid()], PollOptions)?.Code);
    }

    // --- Open ----------------------------------------------------------------------------------------

    [Fact]
    public void The_in_memory_open_rule_agrees_with_the_query_rule()
    {
        var query = TeamPollOpen.At(Now).Compile();
        DateTime?[] times = [null, Now.AddMinutes(-1), Now, Now.AddMinutes(1)];

        foreach (var closedAt in times)
        {
            foreach (var closesAt in times)
            {
                var poll = new JuggerHub.Entities.TeamPoll { ClosedAt = closedAt, ClosesAt = closesAt };
                Assert.Equal(query(poll), TeamPollOpen.IsOpen(closedAt, closesAt, Now));
            }
        }

        Assert.True(TeamPollOpen.IsOpen(null, null, Now));
        Assert.False(TeamPollOpen.IsOpen(null, Now, Now)); // closes at this very moment ⇒ closed
    }
}
