using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Who hears about a poll, on which channel, and what happens to the alerts afterwards (feature 062,
/// spec FR-026 – FR-030, FR-024, SC-004, SC-007). Real API + Postgres container; the email sender,
/// the push dispatcher and the realtime hub are the factory's recording fakes.
/// </summary>
[Collection("Teams")]
public sealed class TeamPollNotificationTests : TeamPollTestSupport
{
    private const string AlertType = "TeamPoll";

    public TeamPollNotificationTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Every_member_but_the_author_gets_one_alert_naming_the_team_and_the_question()
    {
        var team = await TeamWithMembersAsync(2);
        var outsider = await NewUserAsync();

        var poll = await CreatePollAsync(team.Admin, team.Slug, "Which jersey colour?", ["Black", "Orange"]);

        foreach (var member in team.Members)
        {
            var alert = Assert.Single(await AlertsAsync(member));
            var payload = alert.GetProperty("payload");
            Assert.Equal(team.Slug, payload.GetProperty("teamSlug").GetString());
            Assert.Equal(team.Name, payload.GetProperty("teamName").GetString());
            Assert.Equal(PollId(poll).ToString(), payload.GetProperty("pollId").GetString());
            Assert.Equal("Which jersey colour?", payload.GetProperty("question").GetString());
            // No person is named in the payload: the author is the row's actor (037 FR-023).
            Assert.Equal(
                ["pollId", "question", "teamName", "teamSlug"],
                payload.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
            Assert.Equal(team.Admin.Handle, alert.GetProperty("actorDisplayName").GetString());
        }

        Assert.Empty(await AlertsAsync(team.Admin));
        Assert.Empty(await AlertsAsync(outsider));
    }

    [Fact]
    public async Task Each_channel_follows_its_own_team_news_switch()
    {
        var team = await TeamWithMembersAsync(2);
        var (emailOff, inAppOff) = (team.Members[0], team.Members[1]);
        await SetPreferenceAsync(emailOff, "TeamNews", "Email", false);
        await SetPreferenceAsync(inAppOff, "TeamNews", "InApp", false);

        await CreatePollAsync(team.Admin, team.Slug);

        // Email off: the alert and the device notice, no email.
        Assert.Single(await AlertsAsync(emailOff));
        Assert.Empty(EmailsTo(emailOff, team.Name));
        Assert.Single(PushesTo(emailOff));

        // In-app off: no alert — and the device notice still goes (055's independence).
        Assert.Empty(await AlertsAsync(inAppOff));
        Assert.Single(EmailsTo(inAppOff, team.Name));
        Assert.Single(PushesTo(inAppOff));
    }

    [Fact]
    public async Task The_email_is_in_each_recipients_own_language_and_links_to_the_poll()
    {
        var team = await TeamWithMembersAsync(2);
        var (german, english) = (team.Members[0], team.Members[1]);
        await SetLanguageAsync(german, "de");
        await SetLanguageAsync(english, "en");

        var poll = await CreatePollAsync(team.Admin, team.Slug, "Grillen am Samstag?", ["Ja", "Nein"]);

        var de = Assert.Single(EmailsTo(german, team.Name));
        Assert.Equal($"{team.Name} hat eine Umfrage gestartet — JuggerHub", de.Subject);
        Assert.Contains("Grillen am Samstag?", de.HtmlBody, StringComparison.Ordinal);
        Assert.Contains($"/t/{team.Slug}#poll-{PollId(poll)}", de.HtmlBody, StringComparison.Ordinal);

        var en = Assert.Single(EmailsTo(english, team.Name));
        Assert.Equal($"{team.Name} started a poll — JuggerHub", en.Subject);
        // The subject never carries the question: it shows on a locked screen.
        Assert.DoesNotContain("Grillen", en.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_email_escapes_the_question()
    {
        var team = await TeamWithMembersAsync(1);

        await CreatePollAsync(team.Admin, team.Slug, "<script>alert(1)</script> or <b>not</b>?", ["Yes", "No"]);

        var email = Assert.Single(EmailsTo(team.Members[0], team.Name));
        Assert.DoesNotContain("<script>", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", email.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_device_notice_names_the_team_and_never_the_question()
    {
        var team = await TeamWithMembersAsync(1);

        var poll = await CreatePollAsync(team.Admin, team.Slug, "Secret surprise party for Nia?", ["Yes", "No"]);

        var push = Assert.Single(PushesTo(team.Members[0]));
        Assert.Equal(team.Name, push.Content.Title);
        Assert.Equal("Your team started a poll", push.Content.Body);
        Assert.DoesNotContain("surprise", push.Content.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("surprise", push.Content.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"/t/{team.Slug}#poll-{PollId(poll)}", push.Content.Url);
        Assert.Equal($"poll:{PollId(poll)}", push.Content.Tag);
        Assert.DoesNotContain(team.Admin.Id, Factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task A_failed_push_never_fails_starting_the_poll()
    {
        var team = await TeamWithMembersAsync(1);
        Factory.PushDispatcher.ThrowOnDispatch = true;
        try
        {
            var resp = await PostPollAsync(team.Admin, team.Slug);
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }
        finally
        {
            Factory.PushDispatcher.ThrowOnDispatch = false;
        }
    }

    // --- US5: an edit corrects the alerts silently; a delete removes them ----------------------------

    [Fact]
    public async Task Correcting_the_question_rewrites_every_delivered_alert_silently_former_members_included()
    {
        var team = await TeamWithMembersAsync(2);
        var (stays, leaves) = (team.Members[0], team.Members[1]);
        var poll = await CreatePollAsync(team.Admin, team.Slug, "Jersy colour?", ["Black", "Orange"]);
        await LeaveAsync(leaves, team.Slug);

        var before = await RowsAsync(PollId(poll));
        var liveBefore = Factory.NotificationRealtime.CreatedFor(stays.Id).Count;
        var emailsBefore = EmailsTo(stays, team.Name).Count;
        var pushesBefore = PushesTo(stays).Count;

        var resp = await UpdateAsync(team.Admin, team.Slug, PollId(poll), new
        {
            question = "Jersey colour?",
            options = new[] { "Black", "Orange" },
            allowsMultiple = false,
            resultsAfterAnswer = false,
            closesAt = (DateTimeOffset?)null,
        });
        resp.EnsureSuccessStatusCode();

        var after = await RowsAsync(PollId(poll));
        Assert.Equal(before.Keys.ToHashSet(), after.Keys.ToHashSet());
        Assert.Equal(2, after.Count); // the one who left included
        foreach (var (rowId, row) in after)
        {
            Assert.Equal("Jersey colour?", JsonDocument.Parse(row.Payload).RootElement.GetProperty("question").GetString());
            Assert.Equal(before[rowId].IsRead, row.IsRead);
            Assert.Equal(before[rowId].CreatedDate, row.CreatedDate);
            Assert.True(row.ModifiedDate > before[rowId].ModifiedDate);
        }

        Assert.Equal(liveBefore, Factory.NotificationRealtime.CreatedFor(stays.Id).Count);
        Assert.Equal(emailsBefore, EmailsTo(stays, team.Name).Count);
        Assert.Equal(pushesBefore, PushesTo(stays).Count);
    }

    [Fact]
    public async Task Changing_only_the_options_leaves_the_alerts_alone()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug, "Colour?", ["Blak", "Orange"]);
        var before = await RowsAsync(PollId(poll));

        (await UpdateAsync(team.Admin, team.Slug, PollId(poll), new
        {
            question = "Colour?",
            options = new[] { "Black", "Orange" },
            allowsMultiple = false,
            resultsAfterAnswer = false,
            closesAt = (DateTimeOffset?)null,
        })).EnsureSuccessStatusCode();

        var after = await RowsAsync(PollId(poll));
        Assert.All(after, kv => Assert.Equal(before[kv.Key].ModifiedDate, kv.Value.ModifiedDate));
    }

    [Fact]
    public async Task Deleting_a_poll_removes_every_alert_it_produced_and_lowers_the_badges()
    {
        var team = await TeamWithMembersAsync(2);
        var (stays, leaves) = (team.Members[0], team.Members[1]);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        await LeaveAsync(leaves, team.Slug);
        var unreadBefore = await UnreadCountAsync(stays);
        var badgesBefore = Factory.NotificationRealtime.UnreadCountsFor(stays.Id).Count;

        (await DeleteAsync(team.Admin, team.Slug, PollId(poll))).EnsureSuccessStatusCode();

        Assert.Empty(await RowsAsync(PollId(poll)));
        Assert.Empty(await AlertsAsync(stays));
        Assert.Empty(await AlertsAsync(leaves));
        Assert.Equal(unreadBefore - 1, await UnreadCountAsync(stays));
        Assert.True(Factory.NotificationRealtime.UnreadCountsFor(stays.Id).Count > badgesBefore);
    }

    [Fact]
    public async Task Closing_and_answering_send_nothing()
    {
        var team = await TeamWithMembersAsync(1);
        var poll = await CreatePollAsync(team.Admin, team.Slug);
        var totalBefore = await TotalAlertsAsync(team.Admin);
        var pollPushes = () => Factory.PushDispatcher.Dispatches.Count(d => d.Content.Tag == $"poll:{PollId(poll)}");
        var pushesBefore = pollPushes();

        (await AnswerAsync(team.Members[0], team.Slug, PollId(poll), OptionIds(poll)[0])).EnsureSuccessStatusCode();
        (await CloseAsync(team.Admin, team.Slug, PollId(poll))).EnsureSuccessStatusCode();

        Assert.Equal(totalBefore, await TotalAlertsAsync(team.Admin));
        Assert.Single(await AlertsAsync(team.Members[0]));
        Assert.Equal(pushesBefore, pollPushes());
    }

    // --- helpers --------------------------------------------------------------------------------------

    private sealed record AlertRow(string Payload, bool IsRead, DateTime CreatedDate, DateTime ModifiedDate);

    /// <summary>Every recipient's Alerts row for the poll, by row id, found by the prefix they were written under.</summary>
    private Task<Dictionary<Guid, AlertRow>> RowsAsync(Guid pollId) =>
        WithDbAsync(db => db.Notifications.AsNoTracking()
            .Where(n => n.DedupeKey != null && n.DedupeKey.StartsWith($"poll:{pollId}:"))
            .ToDictionaryAsync(n => n.Id, n => new AlertRow(n.Payload, n.IsRead, n.CreatedDate, n.ModifiedDate)));

    private static async Task<List<JsonElement>> AlertsAsync(Player reader)
    {
        var page = await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?take=100");
        return page.GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("type").GetString() == AlertType)
            .Select(n => n.Clone())
            .ToList();
    }

    private static async Task<int> TotalAlertsAsync(Player reader) =>
        (await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).GetProperty("totalCount").GetInt32();

    private static async Task<int> UnreadCountAsync(Player reader) =>
        (await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32();

    /// <summary>Poll emails to the player about this test's team (team names are unique per test).</summary>
    private List<CapturedEmail> EmailsTo(Player player, string teamName) =>
        Factory.EmailSender.Sent
            .Where(e => string.Equals(e.To, player.Email, StringComparison.OrdinalIgnoreCase)
                && e.Subject.Contains(teamName, StringComparison.Ordinal))
            .ToList();

    private List<Push.FakePushDispatcher.Dispatch> PushesTo(Player player) =>
        Factory.PushDispatcher.Dispatches
            .Where(d => d.RecipientUserIds.Contains(player.Id) && d.Content.Tag.StartsWith("poll:", StringComparison.Ordinal))
            .ToList();
}
