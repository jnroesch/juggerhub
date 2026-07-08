using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Notifications;

/// <summary>
/// In-app notifications (feature 010): per-recipient scoping (no cross-user leakage), the unread
/// count + mark-read/-all, and the three producers (team invite with inline resolution, role
/// change, team-news post + fan-out). Exercises the real API + Postgres container. Reuses the
/// shared "Teams" collection fixture since every scenario builds on teams/invites.
/// </summary>
[Collection("Teams")]
public sealed class NotificationTests
{
    private readonly JuggerHubApiFactory _factory;

    public NotificationTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- Scoping & auth -------------------------------------------------------

    [Fact]
    public async Task Notifications_are_scoped_to_the_recipient_and_never_leak()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        var (other, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });

        // The target sees their invite notification.
        var mine = await ListAsync(target);
        Assert.Single(mine.EnumerateArray());
        var notificationId = mine[0].GetProperty("id").GetString()!;

        // An unrelated user sees none of it, and cannot mark it read (404, no existence oracle).
        var others = await ListAsync(other);
        Assert.Empty(others.EnumerateArray());
        var foreignMark = await other.PostAsync($"/api/v1/notifications/{notificationId}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, foreignMark.StatusCode);

        // Anonymous is rejected.
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/notifications")).StatusCode);
    }

    // --- US2: invite producer + inline resolution -----------------------------

    [Fact]
    public async Task Targeted_invite_creates_an_actionable_notification_that_resolves_on_accept()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });

        var before = (await ListAsync(target))[0];
        Assert.Equal("TeamInvite", before.GetProperty("type").GetString());
        Assert.False(before.GetProperty("resolved").GetBoolean());
        Assert.False(before.GetProperty("isRead").GetBoolean());
        Assert.Equal(1, await UnreadCountOf(target));

        var token = before.GetProperty("payload").GetProperty("token").GetString()!;
        var accept = await target.PostAsync($"/api/v1/invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // The row reconciles to the live invite state — no longer actionable.
        var after = (await ListAsync(target))[0];
        Assert.True(after.GetProperty("resolved").GetBoolean());
    }

    [Fact]
    public async Task Re_inviting_the_same_user_does_not_duplicate_the_notification()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });
        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });

        Assert.Single((await ListAsync(target)).EnumerateArray());
    }

    // --- Mark read / read-all -------------------------------------------------

    [Fact]
    public async Task Mark_read_and_mark_all_read_drive_the_unread_count()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });
        var id = (await ListAsync(target))[0].GetProperty("id").GetString()!;
        Assert.Equal(1, await UnreadCountOf(target));

        var mark = await target.PostAsync($"/api/v1/notifications/{id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, mark.StatusCode);
        Assert.Equal(0, await UnreadCountOf(target));

        // Idempotent: marking an already-read own notification is still success.
        var again = await target.PostAsync($"/api/v1/notifications/{id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);

        var markAll = await target.PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, markAll.StatusCode);
        Assert.Equal(0, await UnreadCountOf(target));
    }

    // --- US3: role-change producer --------------------------------------------

    [Fact]
    public async Task Role_change_notifies_the_member_but_not_the_acting_admin()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (member, memberId, _, _) = await NewUserAsync();

        var token = (await CreateLinkAsync(admin, slug)).Token;
        await member.PostAsync($"/api/v1/invitations/{token}/accept", null);

        var promote = await admin.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{memberId}/role", new { role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        var memberItems = await ListAsync(member);
        Assert.Contains(memberItems.EnumerateArray(), n => n.GetProperty("type").GetString() == "TeamRoleChanged");

        var adminItems = await ListAsync(admin);
        Assert.DoesNotContain(adminItems.EnumerateArray(), n => n.GetProperty("type").GetString() == "TeamRoleChanged");
    }

    // --- US3: team-news producer + posting authorization ----------------------

    [Fact]
    public async Task Posting_news_notifies_other_members_only_and_non_admins_are_forbidden()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (member, _, _, _) = await NewUserAsync();

        var token = (await CreateLinkAsync(admin, slug)).Token;
        await member.PostAsync($"/api/v1/invitations/{token}/accept", null);

        var post = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body = "Training moves to Saturday 10:00." });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        // The other member is notified; the author is not.
        var memberItems = await ListAsync(member);
        Assert.Contains(memberItems.EnumerateArray(), n => n.GetProperty("type").GetString() == "TeamNews");
        var adminItems = await ListAsync(admin);
        Assert.DoesNotContain(adminItems.EnumerateArray(), n => n.GetProperty("type").GetString() == "TeamNews");

        // A plain member cannot post news.
        var forbidden = await member.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body = "Sneaky post." });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Empty body is rejected by validation.
        var empty = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body = "" });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    // --- helpers --------------------------------------------------------------

    private async Task<JsonElement> ListAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        return page.GetProperty("items").Clone();
    }

    private async Task<int> UnreadCountOf(HttpClient client)
    {
        var dto = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count");
        return dto.GetProperty("count").GetInt32();
    }

    private async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }

    private static string NewSlug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private static Task<HttpResponseMessage> CreateTeamAsync(HttpClient client, string slug) =>
        client.PostAsJsonAsync("/api/v1/teams", new { name = "Rheinfeuer", slug, type = "CityTeam", city = "Berlin" });

    private async Task<(string Token, string Url)> CreateLinkAsync(HttpClient admin, string slug)
    {
        var resp = await admin.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (dto.GetProperty("token").GetString()!, dto.GetProperty("url").GetString()!);
    }
}
