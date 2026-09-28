using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Home;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Events;

/// <summary>
/// Editing and deleting event news (feature 059, GH #367). Any current event admin may act on any
/// post; nothing is sent either way, since event news never notified anyone to begin with. Real
/// API + Postgres container.
/// </summary>
/// <remarks>
/// Dates are only ever compared between two database reads (see <c>TeamNewsEditDeleteTests</c>):
/// a POST response carries 100 ns ticks while Postgres keeps microseconds.
/// </remarks>
[Collection("Events")]
public sealed class EventNewsEditDeleteTests
{
    private readonly JuggerHubApiFactory _factory;

    public EventNewsEditDeleteTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- Editing ----------------------------------------------------------------------------------

    [Fact]
    public async Task A_co_admin_edits_the_creators_post_in_place_and_it_reads_as_edited()
    {
        var ev = await EventWithAdminsAsync();
        await PostAsync(ev.Creator, ev.Id, "Bring a pompfe.");
        var post = await PostAsync(ev.Creator, ev.Id, "Check-in opens at 08:00.");
        var before = await FeedAsync(ev.Reader, ev.Id);
        var modifiedBefore = await ModifiedDateOfPostAsync(post);

        var resp = await EditAsync(ev.CoAdmin, ev.Id, post, "  Check-in opens at 09:00.  ");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var edited = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(post.ToString(), edited.GetProperty("id").GetString());
        Assert.Equal("Check-in opens at 09:00.", edited.GetProperty("body").GetString());
        Assert.Equal(JsonValueKind.String, edited.GetProperty("editedDate").ValueKind);

        // Same position, author and posting date; only the text and the marker changed (FR-004).
        var after = await FeedAsync(ev.Reader, ev.Id);
        Assert.Equal(before.Select(Id), after.Select(Id));
        var item = after.Single(n => Id(n) == post.ToString());
        var original = before.Single(n => Id(n) == post.ToString());
        Assert.Equal("Check-in opens at 09:00.", item.GetProperty("body").GetString());
        Assert.Equal(original.GetProperty("createdDate").GetDateTime(), item.GetProperty("createdDate").GetDateTime());
        Assert.Equal(original.GetProperty("authorDisplayName").GetString(), item.GetProperty("authorDisplayName").GetString());
        Assert.Equal(JsonValueKind.String, item.GetProperty("editedDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, after.Single(n => Id(n) != post.ToString()).GetProperty("editedDate").ValueKind);

        // The write went through ExecuteUpdate, which skips the audit interceptor (Gate 2).
        Assert.True(await ModifiedDateOfPostAsync(post) > modifiedBefore);
    }

    [Fact]
    public async Task Saving_the_same_text_changes_nothing()
    {
        var ev = await EventWithAdminsAsync();
        var post = await PostAsync(ev.Creator, ev.Id, "Schedule posted.");
        var modifiedBefore = await ModifiedDateOfPostAsync(post);

        var resp = await EditAsync(ev.Creator, ev.Id, post, " Schedule posted. ");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("editedDate").ValueKind);
        Assert.Equal(modifiedBefore, await ModifiedDateOfPostAsync(post));
    }

    public static TheoryData<string> BodiesPostingWouldRefuse => new() { "", "   ", new string('x', 2001) };

    [Theory]
    [MemberData(nameof(BodiesPostingWouldRefuse))]
    public async Task An_edit_follows_the_posting_rules(string body)
    {
        var ev = await EventWithAdminsAsync();
        var post = await PostAsync(ev.Creator, ev.Id, "Stays as it is.");

        var resp = await EditAsync(ev.Creator, ev.Id, post, body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("Stays as it is.", (await FeedAsync(ev.Reader, ev.Id)).Single().GetProperty("body").GetString());
    }

    [Fact]
    public async Task Editing_and_deleting_send_nothing()
    {
        var ev = await EventWithAdminsAsync();
        var post = await PostAsync(ev.Creator, ev.Id, "Check-in opens at 08:00.");
        var other = await PostAsync(ev.Creator, ev.Id, "Bring water.");
        var emailsBefore = _factory.EmailSender.Sent.Count;
        var pushesBefore = _factory.PushDispatcher.Recipients.Count;
        var liveBefore = _factory.NotificationRealtime.CreatedFor(ev.Reader.Id).Count;
        var countsBefore = _factory.NotificationRealtime.UnreadCountsFor(ev.Reader.Id).Count;

        (await EditAsync(ev.CoAdmin, ev.Id, post, "Check-in opens at 09:00.")).EnsureSuccessStatusCode();
        (await DeleteAsync(ev.CoAdmin, ev.Id, other)).EnsureSuccessStatusCode();

        Assert.Equal(emailsBefore, _factory.EmailSender.Sent.Count);
        Assert.Equal(pushesBefore, _factory.PushDispatcher.Recipients.Count);
        Assert.Equal(liveBefore, _factory.NotificationRealtime.CreatedFor(ev.Reader.Id).Count);
        Assert.Equal(countsBefore, _factory.NotificationRealtime.UnreadCountsFor(ev.Reader.Id).Count);
        Assert.Equal(0, await TotalNotificationsAsync(ev.Reader));
    }

    [Fact]
    public async Task Only_a_current_admin_of_the_posts_own_event_may_edit_it()
    {
        var ev = await EventWithAdminsAsync();
        var post = await PostAsync(ev.Creator, ev.Id, "Bring water.");

        // Anyone signed in reads event news; reading grants nothing more.
        var readerResp = await EditAsync(ev.Reader, ev.Id, post, "Hijacked.");
        Assert.Equal(HttpStatusCode.Forbidden, readerResp.StatusCode);

        // An unknown event answers as reading its news does.
        var unknownResp = await EditAsync(ev.Creator, Guid.NewGuid(), post, "Hijacked.");
        Assert.Equal(HttpStatusCode.NotFound, unknownResp.StatusCode);
        Assert.Equal("Event not found", await ProblemTitleAsync(unknownResp));

        // A post is reachable only through its own event, even for an admin of both (FR-013).
        var otherEvent = await SeedEventAsync("Other Cup");
        await MakeAdminAsync(otherEvent, ev.Creator);
        var crossResp = await EditAsync(ev.Creator, otherEvent, post, "Hijacked.");
        Assert.Equal(HttpStatusCode.NotFound, crossResp.StatusCode);
        Assert.Equal("News post not found", await ProblemTitleAsync(crossResp));

        // Authorship grants nothing on its own: a former co-admin cannot edit their old post.
        var coAdminPost = await PostAsync(ev.CoAdmin, ev.Id, "Written while an admin.");
        await RemoveAdminAsync(ev.Id, ev.CoAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, (await EditAsync(ev.CoAdmin, ev.Id, coAdminPost, "Changed.")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(ev.CoAdmin, ev.Id, coAdminPost)).StatusCode);

        // None of it changed anything.
        var feed = await FeedAsync(ev.Reader, ev.Id);
        Assert.Equal("Bring water.", feed.Single(n => Id(n) == post.ToString()).GetProperty("body").GetString());
        Assert.Equal("Written while an admin.", feed.Single(n => Id(n) == coAdminPost.ToString()).GetProperty("body").GetString());
    }

    [Fact]
    public async Task Home_marks_an_edited_event_post_and_nothing_never_edited()
    {
        var ev = await EventWithAdminsAsync();
        await HomeTestSupport.SignupUserAsync(_factory, ev.Id, ev.Reader.Id);
        var edited = await PostAsync(ev.Creator, ev.Id, "Check-in opens at 08:00.");
        await PostAsync(ev.Creator, ev.Id, "Never touched.");

        (await EditAsync(ev.CoAdmin, ev.Id, edited, "Check-in opens at 09:00.")).EnsureSuccessStatusCode();

        var news = await HomeNewsAsync(ev.Reader);
        var editedItem = news.Single(n => n.GetProperty("body").GetString() == "Check-in opens at 09:00.");
        Assert.Equal("event", editedItem.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.String, editedItem.GetProperty("editedDate").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            news.Single(n => n.GetProperty("body").GetString() == "Never touched.").GetProperty("editedDate").ValueKind);

        // The dashboard's own News module reads the same item.
        var home = await ev.Reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var dashboardItem = home.GetProperty("news").EnumerateArray()
            .Single(n => n.GetProperty("body").GetString() == "Check-in opens at 09:00.");
        Assert.Equal(JsonValueKind.String, dashboardItem.GetProperty("editedDate").ValueKind);
    }

    // --- Deleting ---------------------------------------------------------------------------------

    [Fact]
    public async Task Deleting_takes_the_post_off_the_event_page_and_off_Home()
    {
        var ev = await EventWithAdminsAsync();
        await HomeTestSupport.SignupUserAsync(_factory, ev.Id, ev.Reader.Id);
        var post = await PostAsync(ev.Creator, ev.Id, "Meant for another tournament.");
        await PostAsync(ev.Creator, ev.Id, "Stays.");
        Assert.Contains(await HomeNewsAsync(ev.Reader), n => n.GetProperty("body").GetString() == "Meant for another tournament.");

        var resp = await DeleteAsync(ev.CoAdmin, ev.Id, post);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal(["Stays."], (await FeedAsync(ev.Reader, ev.Id)).Select(n => n.GetProperty("body").GetString()));
        Assert.DoesNotContain(await HomeNewsAsync(ev.Reader), n => n.GetProperty("body").GetString() == "Meant for another tournament.");

        // Deleting it again: it is simply not there any more.
        var again = await DeleteAsync(ev.CoAdmin, ev.Id, post);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal("News post not found", await ProblemTitleAsync(again));
    }

    [Fact]
    public async Task Delete_is_refused_to_non_admins_and_scoped_to_the_posts_own_event()
    {
        var ev = await EventWithAdminsAsync();
        var post = await PostAsync(ev.Creator, ev.Id, "Bring water.");
        var otherEvent = await SeedEventAsync("Other Cup");
        await MakeAdminAsync(otherEvent, ev.Creator);

        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(ev.Reader, ev.Id, post)).StatusCode);
        Assert.Equal("Event not found", await ProblemTitleAsync(await DeleteAsync(ev.Creator, Guid.NewGuid(), post)));
        Assert.Equal("News post not found", await ProblemTitleAsync(await DeleteAsync(ev.Creator, otherEvent, post)));

        Assert.Single(await FeedAsync(ev.Reader, ev.Id));
    }

    [Fact]
    public async Task A_cancelled_events_news_can_still_be_corrected_and_removed()
    {
        var ev = await EventWithAdminsAsync();
        var edited = await PostAsync(ev.Creator, ev.Id, "See you Saturday.");
        var removed = await PostAsync(ev.Creator, ev.Id, "Wrong event.");
        await HomeTestSupport.WithDbAsync(_factory, db =>
            db.Events.Where(e => e.Id == ev.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EventStatus.Cancelled)));

        (await EditAsync(ev.CoAdmin, ev.Id, edited, "Cancelled — see you next year.")).EnsureSuccessStatusCode();
        (await DeleteAsync(ev.CoAdmin, ev.Id, removed)).EnsureSuccessStatusCode();

        var feed = await FeedAsync(ev.Reader, ev.Id);
        Assert.Equal(["Cancelled — see you next year."], feed.Select(n => n.GetProperty("body").GetString()));
    }

    // --- helpers --------------------------------------------------------------------------------

    private sealed record Player(HttpClient Client, Guid Id);

    /// <summary>An event with its creator and one co-admin, plus a signed-in player with no role in it.</summary>
    private sealed record EventSetup(Guid Id, Player Creator, Player CoAdmin, Player Reader);

    private static string? Id(JsonElement item) => item.GetProperty("id").GetString();

    private async Task<Player> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: AuthTestHelpers.NewHandle());
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return new Player(client, userId);
    }

    private Task<Guid> SeedEventAsync(string name) =>
        HomeTestSupport.SeedEventAsync(_factory, name, DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(11), ParticipantMode.Individuals);

    private async Task<EventSetup> EventWithAdminsAsync()
    {
        var eventId = await SeedEventAsync("Rhein Cup");
        var creator = await NewUserAsync();
        var coAdmin = await NewUserAsync();
        var reader = await NewUserAsync();
        await MakeAdminAsync(eventId, creator);
        await MakeAdminAsync(eventId, coAdmin);
        return new EventSetup(eventId, creator, coAdmin, reader);
    }

    private Task MakeAdminAsync(Guid eventId, Player player) =>
        HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            db.EventAdmins.Add(new EventAdmin { EventId = eventId, UserId = player.Id, AddedDate = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

    private Task RemoveAdminAsync(Guid eventId, Player player) =>
        HomeTestSupport.WithDbAsync(_factory, db =>
            db.EventAdmins.Where(a => a.EventId == eventId && a.UserId == player.Id).ExecuteDeleteAsync());

    private static async Task<Guid> PostAsync(Player admin, Guid eventId, string body)
    {
        var resp = await admin.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/news", new { body });
        resp.EnsureSuccessStatusCode();
        return Guid.Parse((await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
    }

    private static Task<HttpResponseMessage> EditAsync(Player actor, Guid eventId, Guid postId, string body) =>
        actor.Client.PatchAsJsonAsync($"/api/v1/events/{eventId}/news/{postId}", new { body });

    private static Task<HttpResponseMessage> DeleteAsync(Player actor, Guid eventId, Guid postId) =>
        actor.Client.DeleteAsync($"/api/v1/events/{eventId}/news/{postId}");

    private static async Task<List<JsonElement>> FeedAsync(Player reader, Guid eventId)
    {
        var page = await reader.Client.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/news");
        return page.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static async Task<List<JsonElement>> HomeNewsAsync(Player reader)
    {
        var page = await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/home/news?take=100");
        return page.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static async Task<int> TotalNotificationsAsync(Player reader) =>
        (await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).GetProperty("totalCount").GetInt32();

    private static async Task<string?> ProblemTitleAsync(HttpResponseMessage resp) =>
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();

    private Task<DateTime> ModifiedDateOfPostAsync(Guid postId) =>
        HomeTestSupport.WithDbAsync(_factory, db =>
            db.EventNewsPosts.AsNoTracking().Where(n => n.Id == postId).Select(n => n.ModifiedDate).SingleAsync());
}
