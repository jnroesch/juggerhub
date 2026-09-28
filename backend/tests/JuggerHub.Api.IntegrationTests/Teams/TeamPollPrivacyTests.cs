using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// What an anonymous poll and a poll with hidden results may reveal (feature 062, spec FR-012a,
/// FR-016 – FR-019, SC-003, SC-003a).
/// </summary>
/// <remarks>
/// <b>Every assertion reads the raw response body</b>, never a typed DTO: the failure this suite exists
/// to catch is a field the server should never have sent, and deserializing into a type that lacks the
/// field would hide exactly that. For each role — the author, a second admin, a plain member — the body
/// is searched for every OTHER voter's handle and display name.
/// </remarks>
[Collection("Teams")]
public sealed class TeamPollPrivacyTests : TeamPollTestSupport
{
    public TeamPollPrivacyTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task No_response_about_an_anonymous_poll_names_another_voter_to_anyone()
    {
        var team = await TeamWithMembersAsync(3);
        var author = team.Admin;
        var secondAdmin = team.Members[0];
        var (ada, ben) = (team.Members[1], team.Members[2]);
        await SetRoleAsync(author, team.Slug, secondAdmin, "Admin");

        var poll = await CreatePollAsync(author, team.Slug, "Which jersey colour?", ["Black", "Orange"], isAnonymous: true);
        var id = PollId(poll);
        var options = OptionIds(poll);

        var bodies = new List<(string Role, Player Viewer, string Body)>();
        bodies.Add(("ada/answer", ada, await BodyOf(AnswerAsync(ada, team.Slug, id, options[0]))));
        bodies.Add(("ben/answer", ben, await BodyOf(AnswerAsync(ben, team.Slug, id, options[1]))));
        bodies.Add(("admin2/answer", secondAdmin, await BodyOf(AnswerAsync(secondAdmin, team.Slug, id, options[0]))));

        foreach (var viewer in new[] { author, secondAdmin, ada, ben })
        {
            bodies.Add(($"{viewer.Handle}/list", viewer, (await ListAsync(viewer, team.Slug)).RawBody));
        }

        bodies.Add(("author/update", author, await BodyOf(UpdateAsync(author, team.Slug, id, new
        {
            question = "Which jersey colour?",
            options = new[] { "Black", "Orange" },
            allowsMultiple = false,
            resultsAfterAnswer = false,
            closesAt = DateTimeOffset.UtcNow.AddDays(2),
        }))));
        bodies.Add(("author/close", author, await BodyOf(CloseAsync(author, team.Slug, id))));
        foreach (var viewer in new[] { author, secondAdmin, ada })
        {
            bodies.Add(($"{viewer.Handle}/closed-list", viewer, (await ListAsync(viewer, team.Slug, "closed")).RawBody));
        }

        var voters = new[] { ada, ben, secondAdmin };
        var names = new Dictionary<Guid, string>();
        foreach (var voter in voters)
        {
            names[voter.Id] = await DisplayNameAsync(voter.Id);
        }

        foreach (var (role, viewer, body) in bodies)
        {
            using var doc = JsonDocument.Parse(body);
            var polls = doc.RootElement.TryGetProperty("items", out var items)
                ? items.EnumerateArray().Where(p => PollId(p) == id).ToList()
                : [doc.RootElement];
            foreach (var p in polls)
            {
                Assert.All(p.GetProperty("options").EnumerateArray(),
                    o => Assert.Equal(JsonValueKind.Null, o.GetProperty("voters").ValueKind));
                Assert.Equal(JsonValueKind.Null, p.GetProperty("notAnswered").ValueKind);
            }

            foreach (var other in voters.Where(v => v.Id != viewer.Id))
            {
                Assert.False(body.Contains(other.Handle, StringComparison.OrdinalIgnoreCase), $"{role} names {other.Handle}");
                Assert.False(body.Contains(names[other.Id], StringComparison.Ordinal), $"{role} names {names[other.Id]}");
                Assert.False(body.Contains(other.Id.ToString(), StringComparison.OrdinalIgnoreCase), $"{role} carries {other.Id}");
            }
        }

        // Each member still sees their own answer, and only theirs.
        var adaView = await ViewAsync(ada, team.Slug, id, "closed");
        Assert.Equal([options[0].ToString()], adaView.GetProperty("myOptionIds").EnumerateArray().Select(e => e.GetString()));
        var authorView = await ViewAsync(author, team.Slug, id, "closed");
        Assert.Empty(authorView.GetProperty("myOptionIds").EnumerateArray());
        Assert.Equal(2, CountOf(authorView, 0));
        Assert.Equal(1, CountOf(authorView, 1));
    }

    [Fact]
    public async Task Home_lists_only_the_viewers_own_unanswered_poll()
    {
        var team = await TeamWithMembersAsync(2);
        var (ada, ben) = (team.Members[0], team.Members[1]);
        var poll = await CreatePollAsync(team.Admin, team.Slug, "Anonymous one?", ["Yes", "No"], isAnonymous: true);
        (await AnswerAsync(ada, team.Slug, PollId(poll), OptionIds(poll)[0])).EnsureSuccessStatusCode();

        // Only the Needs-you block: the rest of Home legitimately names teammates ("Ada joined").
        var benNeeds = await NeedsYouRawAsync(ben);
        var adaNeeds = await NeedsYouRawAsync(ada);

        Assert.Contains(PollId(poll).ToString(), benNeeds, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PollId(poll).ToString(), adaNeeds, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ada.Handle, benNeeds, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ada.Id.ToString(), benNeeds, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hidden_results_carry_no_counts_until_the_viewer_answers_the_author_and_admins_included()
    {
        var team = await TeamWithMembersAsync(2);
        var (ada, ben) = (team.Members[0], team.Members[1]);
        var poll = await CreatePollAsync(team.Admin, team.Slug, "Pizza or pasta?", ["Pizza", "Pasta"], resultsAfterAnswer: true);
        var id = PollId(poll);
        var options = OptionIds(poll);
        (await AnswerAsync(ada, team.Slug, id, options[0])).EnsureSuccessStatusCode();

        // Ben has not answered; neither has the author, who is an admin. Neither sees the result.
        foreach (var viewer in new[] { ben, team.Admin })
        {
            var list = await ListAsync(viewer, team.Slug);
            var view = list.Items.Single(p => PollId(p) == id);
            Assert.False(view.GetProperty("resultsVisible").GetBoolean());
            Assert.All(view.GetProperty("options").EnumerateArray(), o =>
            {
                Assert.Equal(JsonValueKind.Null, o.GetProperty("count").ValueKind);
                Assert.Equal(JsonValueKind.Null, o.GetProperty("voters").ValueKind);
            });
            Assert.Equal(1, view.GetProperty("answeredCount").GetInt32());
            Assert.DoesNotContain("\"count\":1", list.RawBody, StringComparison.Ordinal);
            Assert.DoesNotContain(ada.Handle, VoterSection(list.RawBody), StringComparison.OrdinalIgnoreCase);
        }

        // Answering shows it.
        var answered = await (await AnswerAsync(ben, team.Slug, id, options[1])).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(answered.GetProperty("resultsVisible").GetBoolean());
        Assert.Equal(1, CountOf(answered, 0));
        Assert.Equal(1, CountOf(answered, 1));

        // Withdrawing hides it again.
        var withdrawn = await (await WithdrawAsync(ben, team.Slug, id)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(withdrawn.GetProperty("resultsVisible").GetBoolean());
        Assert.Null(CountOf(withdrawn, 0));

        // Once it closes, everyone sees it.
        (await CloseAsync(team.Admin, team.Slug, id)).EnsureSuccessStatusCode();
        var closed = await ViewAsync(ben, team.Slug, id, "closed");
        Assert.True(closed.GetProperty("resultsVisible").GetBoolean());
        Assert.Equal(1, CountOf(closed, 0));
    }

    [Fact]
    public async Task Anonymity_cannot_be_switched_off_by_an_edit()
    {
        var team = await TeamWithMembersAsync(0);
        var poll = await CreatePollAsync(team.Admin, team.Slug, isAnonymous: true);

        var resp = await UpdateAsync(team.Admin, team.Slug, PollId(poll), new
        {
            question = "Thursday instead of Tuesday this week?",
            options = new[] { "Thursday works", "Stay on Tuesday", "Either is fine" },
            allowsMultiple = false,
            resultsAfterAnswer = false,
            closesAt = (DateTimeOffset?)null,
            isAnonymous = false,
        });

        resp.EnsureSuccessStatusCode();
        Assert.True((await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isAnonymous").GetBoolean());
        Assert.True((await ViewAsync(team.Admin, team.Slug, PollId(poll))).GetProperty("isAnonymous").GetBoolean());
    }

    // --- helpers --------------------------------------------------------------------------------------

    private static async Task<string> NeedsYouRawAsync(Player viewer)
    {
        using var home = JsonDocument.Parse(await viewer.Client.GetStringAsync("/api/v1/home"));
        return home.RootElement.GetProperty("needsYou").GetRawText();
    }

    private static async Task<string> BodyOf(Task<HttpResponseMessage> call)
    {
        var resp = await call;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    /// <summary>The body with the author fields cut out, so a check for voters is not tripped by the byline.</summary>
    private static string VoterSection(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        return string.Join('\n', doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(p => p.GetProperty("options").GetRawText() + p.GetProperty("notAnswered").GetRawText()));
    }

    private Task<string> DisplayNameAsync(Guid userId) =>
        WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.PlayerProfiles.Where(p => p.UserId == userId).Select(p => p.DisplayName)));
}
