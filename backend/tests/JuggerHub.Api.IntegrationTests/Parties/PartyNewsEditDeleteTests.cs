using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Home;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Parties;

/// <summary>
/// Editing and deleting party news (feature 059, GH #368). Any current party admin may act on any
/// post. A party-news alert quotes none of the post, so an edit leaves the alerts exactly as they
/// are; a delete takes them with it, for people who have left the crew too. Real API + Postgres
/// container.
/// </summary>
[Collection("Parties")]
public sealed class PartyNewsEditDeleteTests : PartyTestSupport
{
    public PartyNewsEditDeleteTests(JuggerHubApiFactory factory)
        : base(factory)
    {
    }

    // --- Editing ----------------------------------------------------------------------------------

    [Fact]
    public async Task A_co_admin_edits_the_creators_post_in_place_and_it_reads_as_edited()
    {
        var party = await PartyWithCrewAsync();
        await PostAsync(party.Admin, party.Id, "Kit check on Friday.");
        var post = await PostAsync(party.Admin, party.Id, "Meet 07:00 at the Aral on the A7.");
        await MakePartyAdminAsync(party.Id, party.CoAdmin);
        var before = await FeedAsync(party.Crew, party.Id);
        var modifiedBefore = await ModifiedDateOfPostAsync(post);

        var resp = await EditAsync(party.CoAdmin, party.Id, post, "  Meet 07:00 at the Aral on the A1.  ");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var edited = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Meet 07:00 at the Aral on the A1.", edited.GetProperty("body").GetString());
        Assert.Equal(JsonValueKind.String, edited.GetProperty("editedDate").ValueKind);

        var after = await FeedAsync(party.Crew, party.Id);
        Assert.Equal(before.Select(Id), after.Select(Id));
        var item = after.Single(n => Id(n) == post.ToString());
        var original = before.Single(n => Id(n) == post.ToString());
        Assert.Equal(original.GetProperty("createdDate").GetDateTime(), item.GetProperty("createdDate").GetDateTime());
        Assert.Equal(original.GetProperty("authorDisplayName").GetString(), item.GetProperty("authorDisplayName").GetString());
        Assert.Equal(JsonValueKind.String, item.GetProperty("editedDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, after.Single(n => Id(n) != post.ToString()).GetProperty("editedDate").ValueKind);

        // ExecuteUpdate skips the audit interceptor (Gate 2).
        Assert.True(await ModifiedDateOfPostAsync(post) > modifiedBefore);
    }

    [Fact]
    public async Task An_edit_leaves_the_posts_alerts_untouched_and_sends_nothing()
    {
        var party = await PartyWithCrewAsync();
        var post = await PostAsync(party.Admin, party.Id, "Meet 07:00 at the Aral on the A7.");
        var alertsBefore = await AlertRowsAsync(post);
        Assert.Equal(2, alertsBefore.Count); // the crew and the co-admin-to-be, never the author
        var emailsBefore = EmailsTo(party.Crew);
        var pushesBefore = PushesTo(party.Crew);
        var liveBefore = Factory.NotificationRealtime.CreatedFor(party.Crew.Id).Count;
        var countsBefore = Factory.NotificationRealtime.UnreadCountsFor(party.Crew.Id).Count;
        // The post itself did reach them, so the silence below means something.
        Assert.True(emailsBefore > 0);
        Assert.True(pushesBefore > 0);

        (await EditAsync(party.Admin, party.Id, post, "Meet 07:00 at the Aral on the A1.")).EnsureSuccessStatusCode();

        // The rows quote nothing the admin wrote, so there is nothing in them to correct: not the
        // payload, not the read state, not even the audit field (FR-006).
        Assert.Equal(alertsBefore, await AlertRowsAsync(post));
        Assert.Equal(emailsBefore, EmailsTo(party.Crew));
        Assert.Equal(pushesBefore, PushesTo(party.Crew));
        Assert.Equal(liveBefore, Factory.NotificationRealtime.CreatedFor(party.Crew.Id).Count);
        Assert.Equal(countsBefore, Factory.NotificationRealtime.UnreadCountsFor(party.Crew.Id).Count);
    }

    [Fact]
    public async Task Saving_the_same_text_changes_nothing()
    {
        var party = await PartyWithCrewAsync();
        var post = await PostAsync(party.Admin, party.Id, "Bring water.");
        var modifiedBefore = await ModifiedDateOfPostAsync(post);

        var resp = await EditAsync(party.Admin, party.Id, post, "Bring water. ");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("editedDate").ValueKind);
        Assert.Equal(modifiedBefore, await ModifiedDateOfPostAsync(post));
    }

    public static TheoryData<string> BodiesPostingWouldRefuse => new() { "", "   ", new string('x', 1001) };

    [Theory]
    [MemberData(nameof(BodiesPostingWouldRefuse))]
    public async Task An_edit_follows_the_posting_rules(string body)
    {
        var party = await PartyWithCrewAsync();
        var post = await PostAsync(party.Admin, party.Id, "Stays as it is.");

        Assert.Equal(HttpStatusCode.BadRequest, (await EditAsync(party.Admin, party.Id, post, body)).StatusCode);
        Assert.Equal("Stays as it is.", (await FeedAsync(party.Crew, party.Id)).Single().GetProperty("body").GetString());
    }

    [Fact]
    public async Task Outsiders_get_the_feeds_answer_and_crew_members_are_refused()
    {
        var party = await PartyWithCrewAsync();
        var post = await PostAsync(party.Admin, party.Id, "Bring water.");
        var stranger = await PlayerAsync();

        // Crew, but not a party admin: refused.
        Assert.Equal(HttpStatusCode.Forbidden, (await EditAsync(party.Crew, party.Id, post, "Hijacked.")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(party.Crew, party.Id, post)).StatusCode);

        // Not in the crew — a team member who declined, and a stranger — get exactly what reading the
        // feed gives them, so the party's existence is not confirmed to them (SC-004).
        foreach (var outsider in new[] { party.Bench, stranger })
        {
            var feed = await outsider.Client.GetAsync($"/api/v1/parties/{party.Id}/news");
            Assert.Equal(HttpStatusCode.NotFound, feed.StatusCode);
            var feedTitle = await ProblemTitleAsync(feed);

            var edit = await EditAsync(outsider, party.Id, post, "Hijacked.");
            Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
            Assert.Equal(feedTitle, await ProblemTitleAsync(edit));

            var delete = await DeleteAsync(outsider, party.Id, post);
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
            Assert.Equal(feedTitle, await ProblemTitleAsync(delete));
        }

        // A party that does not exist answers the same way.
        var unknown = await EditAsync(party.Admin, Guid.NewGuid(), post, "Hijacked.");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Party not found", await ProblemTitleAsync(unknown));

        Assert.Equal("Bring water.", (await FeedAsync(party.Crew, party.Id)).Single().GetProperty("body").GetString());
    }

    [Fact]
    public async Task A_post_is_reachable_only_through_its_own_party()
    {
        var party = await PartyWithCrewAsync();
        var post = await PostAsync(party.Admin, party.Id, "Bring water.");
        // The same admin runs a second party, for another event.
        var otherEvent = await CreateTeamsEventAsync(party.Admin.Client);
        var otherParty = await FormPartyAsync(party.Admin.Client, otherEvent, party.TeamId);

        var edit = await EditAsync(party.Admin, otherParty, post, "Hijacked.");
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal("News post not found", await ProblemTitleAsync(edit));
        var delete = await DeleteAsync(party.Admin, otherParty, post);
        Assert.Equal("News post not found", await ProblemTitleAsync(delete));

        // Nothing moved: not the post, not its alerts.
        Assert.Single(await FeedAsync(party.Crew, party.Id));
        Assert.Equal(2, (await AlertRowsAsync(post)).Count);
    }

    // --- Deleting ---------------------------------------------------------------------------------

    [Fact]
    public async Task Deleting_takes_the_post_and_its_alerts_with_it_for_former_crew_too()
    {
        var party = await PartyWithCrewAsync();
        var post = await PostAsync(party.Admin, party.Id, "Meant for the other party.");
        var kept = await PostAsync(party.Admin, party.Id, "Bring water.");
        Assert.Contains(await HomeNewsAsync(party.Crew), n => n.GetProperty("body").GetString() == "Meant for the other party.");
        // The co-admin-to-be reads their alert; the crew member leaves theirs unread and then leaves.
        await MarkAlertReadAsync(party.CoAdmin, post);
        (await party.Crew.Client.PostAsync($"/api/v1/parties/{party.Id}/decline", null)).EnsureSuccessStatusCode();
        var unreadBefore = await UnreadCountAsync(party.Crew);
        var countsBefore = Factory.NotificationRealtime.UnreadCountsFor(party.Crew.Id).Count;
        var readerCountsBefore = Factory.NotificationRealtime.UnreadCountsFor(party.CoAdmin.Id).Count;

        var resp = await DeleteAsync(party.Admin, party.Id, post);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal(["Bring water."], (await FeedAsync(party.CoAdmin, party.Id)).Select(n => n.GetProperty("body").GetString()));
        Assert.DoesNotContain(await HomeNewsAsync(party.CoAdmin), n => n.GetProperty("body").GetString() == "Meant for the other party.");

        // Every alert the post produced is gone — the former crew member's included — and the other
        // post's alerts are untouched.
        Assert.Empty(await AlertRowsAsync(post));
        Assert.Equal(2, (await AlertRowsAsync(kept)).Count);

        // The unread one lowered its owner's count, pushed live; the read one moved no badge.
        Assert.Equal(unreadBefore - 1, await UnreadCountAsync(party.Crew));
        var pushed = Factory.NotificationRealtime.UnreadCountsFor(party.Crew.Id);
        Assert.Equal(countsBefore + 1, pushed.Count);
        Assert.Equal(unreadBefore - 1, pushed[^1]);
        Assert.Equal(readerCountsBefore, Factory.NotificationRealtime.UnreadCountsFor(party.CoAdmin.Id).Count);

        // Deleting it again: it is simply not there any more.
        var again = await DeleteAsync(party.Admin, party.Id, post);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal("News post not found", await ProblemTitleAsync(again));
    }

    [Fact]
    public async Task Any_party_admin_may_delete_another_admins_post()
    {
        var party = await PartyWithCrewAsync();
        await MakePartyAdminAsync(party.Id, party.CoAdmin);
        var post = await PostAsync(party.CoAdmin, party.Id, "Posted by the co-admin.");

        // The creator removes it; then the co-admin, demoted, cannot touch their own old post.
        (await DeleteAsync(party.Admin, party.Id, post)).EnsureSuccessStatusCode();
        var second = await PostAsync(party.CoAdmin, party.Id, "Also by the co-admin.");
        await SetPartyRoleAsync(party.Id, party.CoAdmin, PartyMemberRole.Member);

        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(party.CoAdmin, party.Id, second)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await EditAsync(party.CoAdmin, party.Id, second, "Changed.")).StatusCode);
        Assert.Equal(["Also by the co-admin."], (await FeedAsync(party.Crew, party.Id)).Select(n => n.GetProperty("body").GetString()));
    }

    // --- helpers --------------------------------------------------------------------------------

    private sealed record Player(HttpClient Client, Guid Id, string Email);

    /// <summary>
    /// A team party: its creator (party admin), two crew members (<c>CoAdmin</c> is promoted by the
    /// tests that need it) and a team member who declined (<c>Bench</c>).
    /// </summary>
    private sealed record PartySetup(Guid Id, Guid TeamId, Player Admin, Player CoAdmin, Player Crew, Player Bench);

    private static string? Id(JsonElement item) => item.GetProperty("id").GetString();

    private async Task<Player> PlayerAsync()
    {
        var (client, userId, _, email) = await NewUserAsync();
        return new Player(client, userId, email);
    }

    private async Task<PartySetup> PartyWithCrewAsync()
    {
        var admin = await PlayerAsync();
        var (teamId, _) = await CreateTeamAsync(admin.Client);
        var coAdmin = await PlayerAsync();
        var crew = await PlayerAsync();
        var bench = await PlayerAsync();
        foreach (var p in new[] { coAdmin, crew, bench })
        {
            await AddTeamMemberAsync(teamId, p.Id);
        }

        var eventId = await CreateTeamsEventAsync(admin.Client);
        var partyId = await FormPartyAsync(admin.Client, eventId, teamId);
        (await coAdmin.Client.PostAsync($"/api/v1/parties/{partyId}/join", null)).EnsureSuccessStatusCode();
        (await crew.Client.PostAsync($"/api/v1/parties/{partyId}/join", null)).EnsureSuccessStatusCode();
        (await bench.Client.PostAsync($"/api/v1/parties/{partyId}/decline", null)).EnsureSuccessStatusCode();
        return new PartySetup(partyId, teamId, admin, coAdmin, crew, bench);
    }

    private Task MakePartyAdminAsync(Guid partyId, Player player) => SetPartyRoleAsync(partyId, player, PartyMemberRole.Admin);

    private Task SetPartyRoleAsync(Guid partyId, Player player, PartyMemberRole role) =>
        HomeTestSupport.WithDbAsync(Factory, db =>
            db.PartyMembers.Where(m => m.PartyId == partyId && m.UserId == player.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, role)));

    private static async Task<Guid> PostAsync(Player admin, Guid partyId, string body)
    {
        var resp = await admin.Client.PostAsJsonAsync($"/api/v1/parties/{partyId}/news", new { body });
        resp.EnsureSuccessStatusCode();
        return Guid.Parse((await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
    }

    private static Task<HttpResponseMessage> EditAsync(Player actor, Guid partyId, Guid postId, string body) =>
        actor.Client.PatchAsJsonAsync($"/api/v1/parties/{partyId}/news/{postId}", new { body });

    private static Task<HttpResponseMessage> DeleteAsync(Player actor, Guid partyId, Guid postId) =>
        actor.Client.DeleteAsync($"/api/v1/parties/{partyId}/news/{postId}");

    private static async Task<List<JsonElement>> FeedAsync(Player reader, Guid partyId)
    {
        var page = await reader.Client.GetFromJsonAsync<JsonElement>($"/api/v1/parties/{partyId}/news");
        return page.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static async Task<List<JsonElement>> HomeNewsAsync(Player reader)
    {
        var page = await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/home/news?take=100");
        return page.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static async Task<int> UnreadCountAsync(Player reader) =>
        (await reader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32();

    private async Task MarkAlertReadAsync(Player reader, Guid postId)
    {
        var id = await HomeTestSupport.WithDbAsync(Factory, db =>
            db.Notifications.AsNoTracking()
                .Where(n => n.RecipientUserId == reader.Id && n.DedupeKey == $"party-news:{postId}:{reader.Id}")
                .Select(n => n.Id)
                .SingleAsync());
        (await reader.Client.PostAsync($"/api/v1/notifications/{id}/read", null)).EnsureSuccessStatusCode();
    }

    private static async Task<string?> ProblemTitleAsync(HttpResponseMessage resp) =>
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();

    /// <summary>Party-update emails only: every player also received a verification email.</summary>
    private int EmailsTo(Player player) =>
        Factory.EmailSender.Sent.Count(e =>
            string.Equals(e.To, player.Email, StringComparison.OrdinalIgnoreCase) && e.Subject.Contains("party update"));

    private int PushesTo(Player player) => Factory.PushDispatcher.Recipients.Count(r => r == player.Id);

    private Task<DateTime> ModifiedDateOfPostAsync(Guid postId) =>
        HomeTestSupport.WithDbAsync(Factory, db =>
            db.PartyNewsPosts.AsNoTracking().Where(n => n.Id == postId).Select(n => n.ModifiedDate).SingleAsync());

    /// <summary>Every PartyNews alert row one post produced, as a comparable snapshot, by recipient.</summary>
    private Task<List<string>> AlertRowsAsync(Guid postId) =>
        HomeTestSupport.WithDbAsync(Factory, async db =>
            (await db.Notifications.AsNoTracking()
                .Where(n => n.Type == NotificationType.PartyNews && n.DedupeKey != null && n.DedupeKey.StartsWith($"party-news:{postId}:"))
                .OrderBy(n => n.RecipientUserId)
                .Select(n => new { n.RecipientUserId, n.Payload, n.IsRead, n.CreatedDate, n.ModifiedDate })
                .ToListAsync())
            .Select(n => $"{n.RecipientUserId}|{n.Payload}|{n.IsRead}|{n.CreatedDate:O}|{n.ModifiedDate:O}")
            .ToList());
}
