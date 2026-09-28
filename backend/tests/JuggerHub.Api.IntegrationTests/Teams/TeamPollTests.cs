using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Team polls (feature 062): starting, answering, closing, changing and deleting, and who may do each.
/// Anonymity and hidden results have their own suite (<see cref="TeamPollPrivacyTests"/>), and so do the
/// notices (<see cref="TeamPollNotificationTests"/>). Real API + Postgres container.
/// </summary>
[Collection("Teams")]
public sealed class TeamPollTests : TeamPollTestSupport
{
    public TeamPollTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- US1: an admin asks ---------------------------------------------------------------------------

    [Fact]
    public async Task An_admin_starts_a_poll_and_it_heads_the_open_list()
    {
        var team = await TeamWithMembersAsync(2);

        var poll = await CreatePollAsync(team.Admin, team.Slug, "  Which jersey colour?  ", ["Black", "Orange", "Teal"]);

        Assert.Equal("Which jersey colour?", poll.GetProperty("question").GetString());
        Assert.Equal(["Black", "Orange", "Teal"], poll.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("text").GetString()));
        Assert.True(poll.GetProperty("isOpen").GetBoolean());
        Assert.Equal(JsonValueKind.Null, poll.GetProperty("closedAt").ValueKind);
        Assert.Equal(team.Admin.Handle, poll.GetProperty("authorHandle").GetString());
        Assert.Equal(3, poll.GetProperty("memberCount").GetInt32());
        Assert.Equal(0, poll.GetProperty("answeredCount").GetInt32());
        Assert.False(poll.GetProperty("hasAnswers").GetBoolean());
        Assert.Empty(poll.GetProperty("myOptionIds").EnumerateArray());

        var older = await CreatePollAsync(team.Admin, team.Slug, "Older?");
        var list = await ListAsync(team.Members[0], team.Slug);
        Assert.Equal([PollId(older), PollId(poll)], list.Items.Select(PollId));
    }

    [Fact]
    public async Task Only_an_admin_may_start_a_poll_and_an_outsider_learns_nothing()
    {
        var team = await TeamWithMembersAsync(1);
        var outsider = await NewUserAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await PostPollAsync(team.Members[0], team.Slug)).StatusCode);

        // A non-member and an unknown team get the same answer, for reading and for writing: no oracle
        // for whether the team has polls (FR-032).
        var outsiderPost = await PostPollAsync(outsider, team.Slug);
        Assert.Equal(HttpStatusCode.NotFound, outsiderPost.StatusCode);
        Assert.Equal("Team not found", await ProblemTitleAsync(outsiderPost));

        await CreatePollAsync(team.Admin, team.Slug);
        var outsiderList = await outsider.Client.GetAsync($"/api/v1/teams/{team.Slug}/polls?state=open");
        var unknownList = await outsider.Client.GetAsync("/api/v1/teams/no-such-team-x1/polls?state=open");
        Assert.Equal(HttpStatusCode.NotFound, outsiderList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownList.StatusCode);
        Assert.Equal(await ProblemTitleAsync(unknownList), await ProblemTitleAsync(outsiderList));
    }

    [Theory]
    [InlineData("", "question")]
    [InlineData("   ", "question")]
    public async Task A_question_with_no_words_is_refused_with_a_code(string question, string code)
    {
        var team = await TeamWithMembersAsync(0);

        var resp = await PostPollAsync(team.Admin, team.Slug, question);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal(code, await ProblemCodeAsync(resp));
    }

    [Fact]
    public async Task Refusals_carry_codes_and_the_option_they_are_about()
    {
        var team = await TeamWithMembersAsync(0);

        var tooLong = await PostPollAsync(team.Admin, team.Slug, new string('q', 201));
        Assert.Equal("question", await ProblemCodeAsync(tooLong));

        var one = await PostPollAsync(team.Admin, team.Slug, options: ["Only"]);
        Assert.Equal("optionCount", await ProblemCodeAsync(one));

        var duplicate = await PostPollAsync(team.Admin, team.Slug, options: ["Rot", " rot "]);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var body = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("optionDuplicate", body.GetProperty("code").GetString());
        Assert.Equal(1, body.GetProperty("option").GetInt32());

        var past = await PostPollAsync(team.Admin, team.Slug, closesAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal("closesAtPast", await ProblemCodeAsync(past));

        var far = await PostPollAsync(team.Admin, team.Slug, closesAt: DateTimeOffset.UtcNow.AddDays(400));
        Assert.Equal("closesAtTooFar", await ProblemCodeAsync(far));
    }

    [Fact]
    public async Task A_team_can_have_at_most_ten_open_polls()
    {
        var team = await TeamWithMembersAsync(0);
        for (var i = 0; i < 10; i++)
        {
            await CreatePollAsync(team.Admin, team.Slug, $"Question {i}?");
        }

        var eleventh = await PostPollAsync(team.Admin, team.Slug, "One too many?");

        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
        Assert.Equal("tooManyOpen", await ProblemCodeAsync(eleventh));

        // Closing one frees a place.
        var first = (await ListAsync(team.Admin, team.Slug)).Items[^1];
        (await CloseAsync(team.Admin, team.Slug, PollId(first))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await PostPollAsync(team.Admin, team.Slug, "Now there is room?")).StatusCode);
    }

    // --- US1: the team answers ------------------------------------------------------------------------

    [Fact]
    public async Task A_member_answers_changes_and_withdraws_and_is_counted_once()
    {
        var team = await TeamWithMembersAsync(2);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        var id = PollId(poll);
        var options = OptionIds(poll);
        var (ada, ben) = (team.Members[0], team.Members[1]);

        var first = await AnswerAsync(ada, team.Slug, id, options[0]);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var afterFirst = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([options[0].ToString()], afterFirst.GetProperty("myOptionIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(1, CountOf(afterFirst, 0));

        (await AnswerAsync(ben, team.Slug, id, options[1])).EnsureSuccessStatusCode();

        // Changing moves the answer; it is never counted twice.
        var moved = await (await AnswerAsync(ada, team.Slug, id, options[1])).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, CountOf(moved, 0));
        Assert.Equal(2, CountOf(moved, 1));
        Assert.Equal(2, moved.GetProperty("answeredCount").GetInt32());

        var withdrawn = await WithdrawAsync(ada, team.Slug, id);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        var afterWithdraw = await withdrawn.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(afterWithdraw.GetProperty("myOptionIds").EnumerateArray());
        Assert.Equal(1, CountOf(afterWithdraw, 1));
        Assert.Equal(1, afterWithdraw.GetProperty("answeredCount").GetInt32());

        // Withdrawing nothing is not an error.
        Assert.Equal(HttpStatusCode.OK, (await WithdrawAsync(ada, team.Slug, id)).StatusCode);
    }

    [Fact]
    public async Task A_several_answer_poll_counts_the_member_once_and_every_option_they_chose()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug, "Which days work?", ["Mon", "Wed", "Fri"], allowsMultiple: true);
        var options = OptionIds(poll);

        var resp = await AnswerAsync(team.Members[0], team.Slug, PollId(poll), options[0], options[2], options[2]);

        var view = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new int?[] { 1, 0, 1 }, new[] { CountOf(view, 0), CountOf(view, 1), CountOf(view, 2) });
        Assert.Equal(1, view.GetProperty("answeredCount").GetInt32());
        Assert.Equal(2, view.GetProperty("myOptionIds").GetArrayLength());
    }

    [Fact]
    public async Task An_answer_must_fit_the_poll()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        var other = await CreatePollAsync(team.Admin, team.Slug, "Another?");
        var options = OptionIds(poll);
        var voter = team.Members[0];

        var two = await AnswerAsync(voter, team.Slug, PollId(poll), options[0], options[1]);
        Assert.Equal(HttpStatusCode.BadRequest, two.StatusCode);
        Assert.Equal("choiceCount", await ProblemCodeAsync(two));

        var none = await AnswerAsync(voter, team.Slug, PollId(poll));
        Assert.Equal("choiceCount", await ProblemCodeAsync(none));

        var foreign = await AnswerAsync(voter, team.Slug, PollId(poll), OptionIds(other)[0]);
        Assert.Equal("choiceUnknown", await ProblemCodeAsync(foreign));

        Assert.Equal(0, await VoteRowsAsync(PollId(poll)));
    }

    [Fact]
    public async Task Ten_answers_at_once_from_one_member_leave_exactly_one()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        var options = OptionIds(poll);
        var voter = team.Members[0];

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(i => AnswerAsync(voter, team.Slug, PollId(poll), options[i % options.Length])));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await VoteRowsAsync(PollId(poll)));
    }

    [Fact]
    public async Task A_named_poll_shows_who_chose_what_and_admins_see_who_has_not_answered()
    {
        var team = await TeamWithMembersAsync(2);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        var options = OptionIds(poll);
        var (ada, ben) = (team.Members[0], team.Members[1]);
        (await AnswerAsync(ada, team.Slug, PollId(poll), options[0])).EnsureSuccessStatusCode();

        var asMember = await ViewAsync(ben, team.Slug, PollId(poll));
        Assert.Equal(new string?[] { ada.Handle }, VoterHandles(asMember, 0)!);
        Assert.Empty(VoterHandles(asMember, 1)!);
        // Plain members see the count, never the list (FR-014a).
        Assert.Null(NotAnsweredHandles(asMember));

        var asAdmin = await ViewAsync(team.Admin, team.Slug, PollId(poll));
        Assert.Equal(new HashSet<string?> { team.Admin.Handle, ben.Handle }, NotAnsweredHandles(asAdmin)!.ToHashSet());
    }

    [Fact]
    public async Task A_member_who_leaves_stops_counting_and_counts_again_on_return()
    {
        var team = await TeamWithMembersAsync(2);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        var options = OptionIds(poll);
        var ada = team.Members[0];
        (await AnswerAsync(ada, team.Slug, PollId(poll), options[0])).EnsureSuccessStatusCode();

        await LeaveAsync(ada, team.Slug);

        var afterLeaving = await ViewAsync(team.Admin, team.Slug, PollId(poll));
        Assert.Equal(0, CountOf(afterLeaving, 0));
        Assert.Empty(VoterHandles(afterLeaving, 0)!);
        Assert.Equal(0, afterLeaving.GetProperty("answeredCount").GetInt32());
        Assert.Equal(2, afterLeaving.GetProperty("memberCount").GetInt32());
        // The answer is still stored, so the content stays locked (plan residual).
        Assert.True(afterLeaving.GetProperty("hasAnswers").GetBoolean());

        await JoinAsync(team.Admin, team.Slug, ada);

        var afterReturning = await ViewAsync(team.Admin, team.Slug, PollId(poll));
        Assert.Equal(1, CountOf(afterReturning, 0));
        Assert.Equal(new string?[] { ada.Handle }, VoterHandles(afterReturning, 0)!);
    }

    [Fact]
    public async Task Reading_polls_writes_nothing()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        var before = await ModifiedDateAsync(PollId(poll));

        await ListAsync(team.Members[0], team.Slug);
        await ListAsync(team.Admin, team.Slug);

        Assert.Equal(before, await ModifiedDateAsync(PollId(poll)));
    }

    // --- US4: closing ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_poll_whose_time_has_passed_is_closed_and_refuses_answers()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug, closesAt: DateTimeOffset.UtcNow.AddHours(1));
        var options = OptionIds(poll);
        (await AnswerAsync(team.Members[0], team.Slug, PollId(poll), options[0])).EnsureSuccessStatusCode();

        // The time passes. Nothing sweeps it: the time itself is the closing.
        var closesAt = DateTime.UtcNow.AddMinutes(-1);
        await WithDbAsync(db => db.TeamPolls.Where(p => p.Id == PollId(poll))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ClosesAt, closesAt).SetProperty(p => p.ModifiedDate, DateTime.UtcNow)));

        Assert.DoesNotContain(PollId(poll), (await ListAsync(team.Admin, team.Slug)).Items.Select(PollId));
        var closed = await ViewAsync(team.Admin, team.Slug, PollId(poll), "closed");
        Assert.False(closed.GetProperty("isOpen").GetBoolean());
        Assert.Equal(closed.GetProperty("closesAt").GetDateTime(), closed.GetProperty("closedAt").GetDateTime());
        Assert.Equal(1, CountOf(closed, 0));

        var answer = await AnswerAsync(team.Members[0], team.Slug, PollId(poll), options[1]);
        Assert.Equal(HttpStatusCode.Conflict, answer.StatusCode);
        Assert.Equal("closed", await ProblemCodeAsync(answer));
        var withdraw = await WithdrawAsync(team.Members[0], team.Slug, PollId(poll));
        Assert.Equal("closed", await ProblemCodeAsync(withdraw));
    }

    [Fact]
    public async Task Any_admin_closes_a_poll_early_and_for_good()
    {
        var team = await TeamWithMembersAsync(2);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        var second = team.Members[0];
        await SetRoleAsync(team.Admin, team.Slug, second, "Admin");
        var before = await ModifiedDateAsync(PollId(poll));

        Assert.Equal(HttpStatusCode.Forbidden, (await CloseAsync(team.Members[1], team.Slug, PollId(poll))).StatusCode);

        var resp = await CloseAsync(second, team.Slug, PollId(poll));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var closed = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(closed.GetProperty("isOpen").GetBoolean());
        Assert.Equal(JsonValueKind.String, closed.GetProperty("closedAt").ValueKind);
        // ExecuteUpdate skips the audit interceptor, so this proves ModifiedDate was set by hand (Gate 2).
        Assert.True(await ModifiedDateAsync(PollId(poll)) > before);

        var again = await CloseAsync(team.Admin, team.Slug, PollId(poll));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("closed", await ProblemCodeAsync(again));
    }

    [Fact]
    public async Task Closed_polls_are_listed_most_recently_closed_first()
    {
        var team = await TeamWithMembersAsync(0);
        var a = await CreatePollAsync(team.Admin, team.Slug, "First?");
        var b = await CreatePollAsync(team.Admin, team.Slug, "Second?");
        (await CloseAsync(team.Admin, team.Slug, PollId(b))).EnsureSuccessStatusCode();
        (await CloseAsync(team.Admin, team.Slug, PollId(a))).EnsureSuccessStatusCode();

        var closed = await ListAsync(team.Admin, team.Slug, "closed");

        Assert.Equal([PollId(a), PollId(b)], closed.Items.Select(PollId));
    }

    [Fact]
    public async Task Closing_and_answering_at_once_never_leave_an_answer_after_the_close()
    {
        for (var round = 0; round < 5; round++)
        {
            var team = await TeamWithMembersAsync(1);
            var poll = await CreatePollAsync(team.Admin, team.Slug);
            var option = OptionIds(poll)[0];

            var answering = AnswerAsync(team.Members[0], team.Slug, PollId(poll), option);
            var closing = CloseAsync(team.Admin, team.Slug, PollId(poll));
            await Task.WhenAll(answering, closing);
            var (answer, close) = (await answering, await closing);

            Assert.Equal(HttpStatusCode.OK, close.StatusCode);
            var votes = await VoteRowsAsync(PollId(poll));
            if (answer.StatusCode == HttpStatusCode.OK)
            {
                Assert.Equal(1, votes); // landed before the close
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, answer.StatusCode);
                Assert.Equal(0, votes); // refused after it
            }
        }
    }

    [Fact]
    public async Task The_close_time_moves_while_open_and_the_answers_stay()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug, closesAt: DateTimeOffset.UtcNow.AddDays(1));
        var options = OptionIds(poll);
        (await AnswerAsync(team.Members[0], team.Slug, PollId(poll), options[0])).EnsureSuccessStatusCode();
        var later = DateTimeOffset.UtcNow.AddDays(5);

        var resp = await UpdateAsync(team.Admin, team.Slug, PollId(poll), Unchanged(poll, closesAt: later));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var moved = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(later.UtcDateTime, moved.GetProperty("closesAt").GetDateTime().ToUniversalTime(), TimeSpan.FromSeconds(1));
        // Same option ids and the answer intact: unchanged options are never replaced.
        Assert.Equal(options, OptionIds(moved));
        Assert.Equal(1, CountOf(moved, 0));

        var cleared = await UpdateAsync(team.Admin, team.Slug, PollId(poll), Unchanged(poll, closesAt: null));
        Assert.Equal(JsonValueKind.Null, (await cleared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("closesAt").ValueKind);

        var past = await UpdateAsync(team.Admin, team.Slug, PollId(poll), Unchanged(poll, closesAt: DateTimeOffset.UtcNow.AddMinutes(-5)));
        Assert.Equal("closesAtPast", await ProblemCodeAsync(past));
    }

    // --- US5: correcting and deleting -----------------------------------------------------------------

    [Fact]
    public async Task Before_anyone_answers_an_admin_may_change_everything_but_anonymity()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug, "Jersy colour?", ["Blak", "Orange"]);
        var before = await ModifiedDateAsync(PollId(poll));

        var resp = await UpdateAsync(team.Admin, team.Slug, PollId(poll), new
        {
            question = "Jersey colour?",
            options = new[] { "Black", "Orange", "Teal" },
            allowsMultiple = true,
            resultsAfterAnswer = true,
            closesAt = (DateTimeOffset?)null,
            // Not part of the request: silently ignored, never applied (FR-016).
            isAnonymous = true,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var edited = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Jersey colour?", edited.GetProperty("question").GetString());
        Assert.Equal(3, edited.GetProperty("options").GetArrayLength());
        Assert.True(edited.GetProperty("allowsMultiple").GetBoolean());
        Assert.True(edited.GetProperty("resultsAfterAnswer").GetBoolean());
        Assert.False(edited.GetProperty("isAnonymous").GetBoolean());
        Assert.True(await ModifiedDateAsync(PollId(poll)) > before);
    }

    [Fact]
    public async Task After_the_first_answer_the_content_is_locked()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug, closesAt: DateTimeOffset.UtcNow.AddDays(1));
        (await AnswerAsync(team.Members[0], team.Slug, PollId(poll), OptionIds(poll)[0])).EnsureSuccessStatusCode();

        var resp = await UpdateAsync(team.Admin, team.Slug, PollId(poll), new
        {
            question = "A different question?",
            options = new[] { "Thursday works", "Stay on Tuesday", "Either is fine" },
            allowsMultiple = false,
            resultsAfterAnswer = false,
            closesAt = DateTimeOffset.UtcNow.AddDays(3),
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("answered", await ProblemCodeAsync(resp));
        // All or nothing: the close time sent alongside was not applied either.
        var view = await ViewAsync(team.Admin, team.Slug, PollId(poll));
        Assert.Equal("Thursday instead of Tuesday this week?", view.GetProperty("question").GetString());
        Assert.Equal(poll.GetProperty("closesAt").GetDateTime(), view.GetProperty("closesAt").GetDateTime(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task An_edit_and_a_first_answer_at_once_never_orphan_an_answer()
    {
        for (var round = 0; round < 5; round++)
        {
            var team = await TeamWithMembersAsync(1);
            var poll = await CreatePollAsync(team.Admin, team.Slug, "Q?", ["A", "B"]);
            var oldOption = OptionIds(poll)[0];

            var answering = AnswerAsync(team.Members[0], team.Slug, PollId(poll), oldOption);
            var editing = UpdateAsync(team.Admin, team.Slug, PollId(poll), new
            {
                question = "Q?",
                options = new[] { "A", "B", "C" },
                allowsMultiple = false,
                resultsAfterAnswer = false,
                closesAt = (DateTimeOffset?)null,
            });
            await Task.WhenAll(answering, editing);
            var (answer, edit) = (await answering, await editing);

            if (edit.StatusCode == HttpStatusCode.OK)
            {
                // The edit won: the old option is gone, so the answer to it was refused.
                Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
                Assert.Equal(0, await VoteRowsAsync(PollId(poll)));
            }
            else
            {
                // The answer won: it stands, and the edit was refused.
                Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
                Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
                Assert.Equal(1, await VoteRowsAsync(PollId(poll)));
            }
        }
    }

    [Fact]
    public async Task A_closed_poll_cannot_be_changed()
    {
        var team = await TeamWithMembersAsync(0);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        (await CloseAsync(team.Admin, team.Slug, PollId(poll))).EnsureSuccessStatusCode();

        var resp = await UpdateAsync(team.Admin, team.Slug, PollId(poll), Unchanged(poll, closesAt: DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("closed", await ProblemCodeAsync(resp));
    }

    [Fact]
    public async Task An_admin_deletes_a_poll_with_everything_in_it()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        (await AnswerAsync(team.Members[0], team.Slug, PollId(poll), OptionIds(poll)[0])).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(team.Members[0], team.Slug, PollId(poll))).StatusCode);

        var resp = await DeleteAsync(team.Admin, team.Slug, PollId(poll));

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Empty((await ListAsync(team.Members[0], team.Slug)).Items);
        Assert.Equal(0, await VoteRowsAsync(PollId(poll)));
        Assert.Equal(0, await WithDbAsync(db => db.TeamPollOptions.CountAsync(o => o.PollId == PollId(poll))));

        var again = await DeleteAsync(team.Admin, team.Slug, PollId(poll));
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal("Poll not found", await ProblemTitleAsync(again));
    }

    [Fact]
    public async Task Another_teams_poll_is_not_found_under_this_team()
    {
        var mine = await TeamWithMembersAsync(0);
        var theirs = await TeamWithMembersAsync(0);
        var poll = await CreatePollAsync(theirs.Admin, theirs.Slug);
        // The admin of both teams still cannot reach it through the wrong address.
        await JoinAsync(theirs.Admin, theirs.Slug, mine.Admin);
        await SetRoleAsync(theirs.Admin, theirs.Slug, mine.Admin, "Admin");

        foreach (var resp in new[]
        {
            await CloseAsync(mine.Admin, mine.Slug, PollId(poll)),
            await DeleteAsync(mine.Admin, mine.Slug, PollId(poll)),
            await AnswerAsync(mine.Admin, mine.Slug, PollId(poll), OptionIds(poll)[0]),
        })
        {
            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
            Assert.Equal("Poll not found", await ProblemTitleAsync(resp));
        }

        Assert.True((await ViewAsync(theirs.Admin, theirs.Slug, PollId(poll))).GetProperty("isOpen").GetBoolean());
    }

    // --- helpers --------------------------------------------------------------------------------------

    /// <summary>An update request that repeats the poll's content and sets only the close time.</summary>
    private static object Unchanged(JsonElement poll, DateTimeOffset? closesAt) => new
    {
        question = poll.GetProperty("question").GetString(),
        options = poll.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("text").GetString()).ToArray(),
        allowsMultiple = poll.GetProperty("allowsMultiple").GetBoolean(),
        resultsAfterAnswer = poll.GetProperty("resultsAfterAnswer").GetBoolean(),
        closesAt,
    };

    private Task<int> VoteRowsAsync(Guid pollId) =>
        WithDbAsync(db => db.TeamPollVotes.CountAsync(v => v.PollId == pollId));

    private Task<DateTime> ModifiedDateAsync(Guid pollId) =>
        WithDbAsync(db => db.TeamPolls.AsNoTracking().Where(p => p.Id == pollId).Select(p => p.ModifiedDate).SingleAsync());
}
