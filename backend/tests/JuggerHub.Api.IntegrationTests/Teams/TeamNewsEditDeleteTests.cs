using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Home;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Editing and deleting team news (feature 057). Any current admin may act on any post; an edit
/// notifies nobody and corrects the excerpt the post's alerts carry; a delete takes the post's
/// alerts with it, for former members too. Real API + Postgres container.
/// </summary>
/// <remarks>
/// Dates are only ever compared between two database reads: a POST response carries the
/// in-memory <c>CreatedDate</c> (100 ns ticks) while Postgres keeps microseconds, so comparing
/// across the two would fail on precision alone.
/// </remarks>
[Collection("Teams")]
public sealed class TeamNewsEditDeleteTests
{
    private readonly JuggerHubApiFactory _factory;

    public TeamNewsEditDeleteTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US1: an admin corrects a post ----------------------------------------------------------

    [Fact]
    public async Task An_admin_edits_a_post_in_place_and_it_reads_as_edited()
    {
        var team = await TeamWithMemberAsync();
        await PostAsync(team.Admin, team.Slug, "Kit order closes Friday.");
        var post = await PostAsync(team.Admin, team.Slug, "Training moves to Thursday 19:00.");
        var before = await FeedAsync(team.Member, team.Slug);
        var modifiedBefore = await ModifiedDateOfPostAsync(post);

        var resp = await EditAsync(team.Admin, team.Slug, post, "  Training moves to Friday 19:00.  ");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var edited = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(post.ToString(), edited.GetProperty("id").GetString());
        Assert.Equal("Training moves to Friday 19:00.", edited.GetProperty("body").GetString());
        Assert.Equal(JsonValueKind.String, edited.GetProperty("editedDate").ValueKind);

        // Same position, author and posting date; only the text and the marker changed (FR-004).
        var after = await FeedAsync(team.Member, team.Slug);
        Assert.Equal(before.Select(Id), after.Select(Id));
        var item = after.Single(n => Id(n) == post.ToString());
        var original = before.Single(n => Id(n) == post.ToString());
        Assert.Equal("Training moves to Friday 19:00.", item.GetProperty("body").GetString());
        Assert.Equal(original.GetProperty("createdDate").GetDateTime(), item.GetProperty("createdDate").GetDateTime());
        Assert.Equal(team.Admin.Handle, item.GetProperty("authorHandle").GetString());
        Assert.Equal(JsonValueKind.String, item.GetProperty("editedDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, before.Single(n => Id(n) != post.ToString()).GetProperty("editedDate").ValueKind);

        // The write went through ExecuteUpdate, which skips the audit interceptor (Gate 2).
        Assert.True(await ModifiedDateOfPostAsync(post) > modifiedBefore);
    }

    [Fact]
    public async Task Any_admin_may_edit_a_post_another_admin_wrote()
    {
        var team = await TeamWithMemberAsync();
        var post = await PostAsync(team.Admin, team.Slug, "Pitch booked for Sunday.");
        await SetRoleAsync(team.Admin, team.Slug, team.Member, "Admin");

        var resp = await EditAsync(team.Member, team.Slug, post, "Pitch booked for Saturday.");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var item = (await FeedAsync(team.Admin, team.Slug)).Single(n => Id(n) == post.ToString());
        Assert.Equal("Pitch booked for Saturday.", item.GetProperty("body").GetString());
        // The byline stays the author's; the marker says "edited", not by whom (owner decision).
        Assert.Equal(team.Admin.Handle, item.GetProperty("authorHandle").GetString());
    }

    [Fact]
    public async Task Editing_notifies_nobody()
    {
        var team = await TeamWithMemberAsync();
        var post = await PostAsync(team.Admin, team.Slug, "Training moves to Thursday.");

        var rowsBefore = await TotalNotificationsAsync(team.Member);
        var unreadBefore = await UnreadCountAsync(team.Member);
        var liveBefore = _factory.NotificationRealtime.CreatedFor(team.Member.Id).Count;
        var countsBefore = _factory.NotificationRealtime.UnreadCountsFor(team.Member.Id).Count;
        // The post itself did reach them on every channel, so the silence below means something.
        Assert.Equal(1, NewsEmailsTo(team.Member));
        Assert.Equal(1, PushesTo(team.Member));
        Assert.Equal(1, liveBefore);

        (await EditAsync(team.Admin, team.Slug, post, "Training moves to Friday.")).EnsureSuccessStatusCode();

        Assert.Equal(rowsBefore, await TotalNotificationsAsync(team.Member));
        Assert.Equal(unreadBefore, await UnreadCountAsync(team.Member));
        Assert.Equal(1, NewsEmailsTo(team.Member));
        Assert.Equal(1, PushesTo(team.Member));
        Assert.Equal(liveBefore, _factory.NotificationRealtime.CreatedFor(team.Member.Id).Count);
        Assert.Equal(countsBefore, _factory.NotificationRealtime.UnreadCountsFor(team.Member.Id).Count);
    }

    [Fact]
    public async Task Only_a_current_admin_of_the_posts_own_team_may_edit_it()
    {
        var team = await TeamWithMemberAsync();
        var post = await PostAsync(team.Admin, team.Slug, "Bring water.");
        var outsider = await NewUserAsync();

        // A plain member is refused.
        Assert.Equal(HttpStatusCode.Forbidden, (await EditAsync(team.Member, team.Slug, post, "Hijacked.")).StatusCode);

        // A non-member and an unknown team get the same answer as reading the feed: no oracle.
        var outsiderResp = await EditAsync(outsider, team.Slug, post, "Hijacked.");
        Assert.Equal(HttpStatusCode.NotFound, outsiderResp.StatusCode);
        Assert.Equal("Team not found", await ProblemTitleAsync(outsiderResp));
        var unknownResp = await EditAsync(team.Admin, "no-such-team-" + Guid.NewGuid().ToString("N")[..6], post, "x");
        Assert.Equal(HttpStatusCode.NotFound, unknownResp.StatusCode);
        Assert.Equal("Team not found", await ProblemTitleAsync(unknownResp));

        // A post is reachable only through its own team, even for an admin of both (FR-012).
        var otherSlug = await CreateTeamAsync(team.Admin);
        var crossResp = await EditAsync(team.Admin, otherSlug, post, "Hijacked.");
        Assert.Equal(HttpStatusCode.NotFound, crossResp.StatusCode);
        Assert.Equal("News post not found", await ProblemTitleAsync(crossResp));

        // Authorship grants nothing on its own: a demoted author cannot edit their old post.
        await SetRoleAsync(team.Admin, team.Slug, team.Member, "Admin");
        var memberPost = await PostAsync(team.Member, team.Slug, "Written while an admin.");
        await SetRoleAsync(team.Admin, team.Slug, team.Member, "Member");
        Assert.Equal(HttpStatusCode.Forbidden, (await EditAsync(team.Member, team.Slug, memberPost, "Changed.")).StatusCode);

        // None of it changed anything.
        var feed = await FeedAsync(team.Admin, team.Slug);
        Assert.Equal("Bring water.", feed.Single(n => Id(n) == post.ToString()).GetProperty("body").GetString());
        Assert.Equal("Written while an admin.", feed.Single(n => Id(n) == memberPost.ToString()).GetProperty("body").GetString());
    }

    public static TheoryData<string> BodiesPostingWouldRefuse => new() { "", "   ", new string('x', 1001) };

    [Theory]
    [MemberData(nameof(BodiesPostingWouldRefuse))]
    public async Task An_edit_follows_the_posting_rules(string body)
    {
        var team = await TeamWithMemberAsync();
        var post = await PostAsync(team.Admin, team.Slug, "Original text.");

        var resp = await EditAsync(team.Admin, team.Slug, post, body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var item = (await FeedAsync(team.Admin, team.Slug)).Single(n => Id(n) == post.ToString());
        Assert.Equal("Original text.", item.GetProperty("body").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("editedDate").ValueKind);
    }

    [Fact]
    public async Task Saving_the_same_text_is_not_an_edit()
    {
        var team = await TeamWithMemberAsync();
        var post = await PostAsync(team.Admin, team.Slug, "Same as before.");
        var modifiedBefore = await ModifiedDateOfPostAsync(post);

        var resp = await EditAsync(team.Admin, team.Slug, post, "   Same as before.  ");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("editedDate").ValueKind);
        Assert.Equal(modifiedBefore, await ModifiedDateOfPostAsync(post)); // nothing was written (FR-003)
    }

    [Fact]
    public async Task A_new_post_and_the_feed_carry_the_post_id_and_no_edited_date()
    {
        var team = await TeamWithMemberAsync();

        var created = await team.Admin.Client.PostAsJsonAsync($"/api/v1/teams/{team.Slug}/news", new { body = "Hello, team." });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(dto.GetProperty("id").GetString(), out var id));
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("editedDate").ValueKind);
        Assert.Equal(id.ToString(), Id((await FeedAsync(team.Member, team.Slug)).Single()));
    }

    // --- helpers --------------------------------------------------------------------------------

    private sealed record Player(HttpClient Client, Guid Id, string Handle, string Email);

    private sealed record TeamSetup(string Slug, Player Admin, Player Member);

    private static string? Id(JsonElement item) => item.GetProperty("id").GetString();

    private async Task<Player> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return new Player(client, userId, handle, email);
    }

    private static async Task<string> CreateTeamAsync(Player admin)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        (await admin.Client.PostAsJsonAsync("/api/v1/teams", new
        {
            name = "Rheinfeuer",
            slug,
            type = "CityTeam",
            location = new { cityExternalId = "TEST:berlin" },
        })).EnsureSuccessStatusCode();
        return slug;
    }

    /// <summary>An admin, and a plain member who joined through the team's invite link.</summary>
    private async Task<TeamSetup> TeamWithMemberAsync()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        var member = await NewUserAsync();
        await JoinAsync(admin, slug, member);
        return new TeamSetup(slug, admin, member);
    }

    private static async Task JoinAsync(Player admin, string slug, Player joiner)
    {
        var link = await admin.Client.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        link.EnsureSuccessStatusCode();
        var token = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        (await joiner.Client.PostAsync($"/api/v1/invitations/{token}/accept", null)).EnsureSuccessStatusCode();
    }

    private static async Task LeaveAsync(Player player, string slug) =>
        (await player.Client.DeleteAsync($"/api/v1/teams/{slug}/members/{player.Id}")).EnsureSuccessStatusCode();

    private static async Task SetRoleAsync(Player admin, string slug, Player target, string role) =>
        (await admin.Client.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{target.Id}/role", new { role }))
            .EnsureSuccessStatusCode();

    private static async Task<Guid> PostAsync(Player admin, string slug, string body)
    {
        var resp = await admin.Client.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body });
        resp.EnsureSuccessStatusCode();
        return Guid.Parse((await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
    }

    private static Task<HttpResponseMessage> EditAsync(Player actor, string slug, Guid postId, string body) =>
        actor.Client.PatchAsJsonAsync($"/api/v1/teams/{slug}/news/{postId}", new { body });

    private static Task<HttpResponseMessage> DeleteAsync(Player actor, string slug, Guid postId) =>
        actor.Client.DeleteAsync($"/api/v1/teams/{slug}/news/{postId}");

    private static async Task<List<JsonElement>> FeedAsync(Player reader, string slug)
    {
        var page = await reader.Client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/news");
        return page.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    /// <summary>The reader's Alerts inbox, newest first.</summary>
    private static async Task<List<JsonElement>> AlertsAsync(Player reader)
    {
        var page = await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?take=100");
        return page.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    /// <summary>The reader's Alerts rows announcing <paramref name="postId"/>.</summary>
    private static async Task<List<JsonElement>> AlertsForPostAsync(Player reader, Guid postId) =>
        (await AlertsAsync(reader))
            .Where(n => n.GetProperty("type").GetString() == "TeamNews"
                && n.GetProperty("payload").GetProperty("newsPostId").GetString() == postId.ToString())
            .ToList();

    private static async Task<int> TotalNotificationsAsync(Player reader) =>
        (await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).GetProperty("totalCount").GetInt32();

    private static async Task<int> UnreadCountAsync(Player reader) =>
        (await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32();

    private static async Task MarkReadAsync(Player reader, JsonElement notification) =>
        (await reader.Client.PostAsync($"/api/v1/notifications/{notification.GetProperty("id").GetString()}/read", null))
            .EnsureSuccessStatusCode();

    private static async Task<List<JsonElement>> HomeNewsAsync(Player reader)
    {
        var page = await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/home/news?take=100");
        return page.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static async Task<string?> ProblemTitleAsync(HttpResponseMessage resp) =>
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();

    /// <summary>Team-news emails only: every player also received a verification email.</summary>
    private int NewsEmailsTo(Player player) =>
        _factory.EmailSender.Sent.Count(e =>
            string.Equals(e.To, player.Email, StringComparison.OrdinalIgnoreCase) && e.Subject.StartsWith("News from"));

    private int PushesTo(Player player) => _factory.PushDispatcher.Recipients.Count(r => r == player.Id);

    private Task<DateTime> ModifiedDateOfPostAsync(Guid postId) =>
        HomeTestSupport.WithDbAsync(_factory, db =>
            db.TeamNewsPosts.AsNoTracking().Where(n => n.Id == postId).Select(n => n.ModifiedDate).SingleAsync());
}
