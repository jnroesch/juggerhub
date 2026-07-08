using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Notifications;

/// <summary>
/// Notification preferences (feature 011): per-user scoping, opt-out defaults + upsert round-trip,
/// and enforcement — the In-app toggle suppresses the in-app notification, the Email toggle
/// suppresses that category's email (role change / news / invite), and security email is always
/// sent regardless. Exercises the real API + Postgres + local mail sink. Reuses the "Teams" fixture.
/// </summary>
[Collection("Teams")]
public sealed class PreferenceTests
{
    private readonly JuggerHubApiFactory _factory;

    public PreferenceTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- Scoping, defaults, round-trip ---------------------------------------

    [Fact]
    public async Task Defaults_are_all_on_and_toggles_round_trip_per_user()
    {
        var (user, _, _, _) = await NewUserAsync();

        // Default matrix: every togglable channel is on; the always-on group is present.
        var matrix = await user.GetFromJsonAsync<JsonElement>("/api/v1/notification-preferences");
        Assert.NotEmpty(matrix.GetProperty("alwaysOn").EnumerateArray());
        Assert.True(Channel(matrix, "TeamNews", "email"));
        Assert.True(Channel(matrix, "InvitesAndRoster", "inApp"));

        // Turn Team news → Email off; it persists.
        var put = await user.PutAsJsonAsync("/api/v1/notification-preferences/TeamNews/Email", new { enabled = false });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var after = await user.GetFromJsonAsync<JsonElement>("/api/v1/notification-preferences");
        Assert.False(Channel(after, "TeamNews", "email"));
        Assert.True(Channel(after, "TeamNews", "inApp")); // untouched cell stays on

        // A second user is unaffected (per-user scoping).
        var (other, _, _, _) = await NewUserAsync();
        var otherMatrix = await other.GetFromJsonAsync<JsonElement>("/api/v1/notification-preferences");
        Assert.True(Channel(otherMatrix, "TeamNews", "email"));

        // Anonymous is rejected.
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/notification-preferences")).StatusCode);
    }

    [Fact]
    public async Task Unknown_category_or_channel_is_rejected()
    {
        var (user, _, _, _) = await NewUserAsync();
        var resp = await user.PutAsJsonAsync("/api/v1/notification-preferences/Nonsense/Email", new { enabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // --- In-app enforcement ---------------------------------------------------

    [Fact]
    public async Task In_app_off_suppresses_the_in_app_notification()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        // Target silences the in-app channel for invites & roster.
        await target.PutAsJsonAsync("/api/v1/notification-preferences/InvitesAndRoster/InApp", new { enabled = false });

        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });

        var items = await ListNotificationsAsync(target);
        Assert.Empty(items.EnumerateArray());
    }

    // --- Email enforcement ----------------------------------------------------

    [Fact]
    public async Task Email_off_suppresses_the_invite_email_while_on_sends_it()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        var (onUser, onId, _, onEmail) = await NewUserAsync();       // default: email on
        var (offUser, offId, _, offEmail) = await NewUserAsync();
        await offUser.PutAsJsonAsync("/api/v1/notification-preferences/InvitesAndRoster/Email", new { enabled = false });

        _factory.EmailSender.Clear();
        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = onId });
        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = offId });

        Assert.NotNull(_factory.EmailSender.LatestFor(onEmail));   // on → invite email sent
        Assert.Null(_factory.EmailSender.LatestFor(offEmail));     // off → suppressed
    }

    [Fact]
    public async Task Role_change_and_news_emails_send_when_email_on()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (member, memberId, _, memberEmail) = await NewUserAsync();

        var token = (await CreateLinkAsync(admin, slug)).Token;
        await member.PostAsync($"/api/v1/invitations/{token}/accept", null);

        // Role change → role-change email (email on by default).
        _factory.EmailSender.Clear();
        await admin.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{memberId}/role", new { role = "Admin" });
        Assert.NotNull(_factory.EmailSender.LatestFor(memberEmail));

        // News post → news email; then turn Team news → Email off and confirm suppression.
        _factory.EmailSender.Clear();
        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body = "First update." });
        Assert.NotNull(_factory.EmailSender.LatestFor(memberEmail));

        await member.PutAsJsonAsync("/api/v1/notification-preferences/TeamNews/Email", new { enabled = false });
        _factory.EmailSender.Clear();
        await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body = "Second update." });
        Assert.Null(_factory.EmailSender.LatestFor(memberEmail));
    }

    // --- Security email is exempt --------------------------------------------

    [Fact]
    public async Task Security_email_is_always_sent_regardless_of_preferences()
    {
        var (user, _, _, email) = await NewUserAsync();

        // Turn every togglable channel off.
        foreach (var category in new[] { "InvitesAndRoster", "TeamNews" })
        {
            foreach (var channel in new[] { "InApp", "Email" })
            {
                await user.PutAsJsonAsync($"/api/v1/notification-preferences/{category}/{channel}", new { enabled = false });
            }
        }

        _factory.EmailSender.Clear();
        var forgot = await user.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        // Password reset is security mail — never governed by notification preferences.
        Assert.NotNull(_factory.EmailSender.LatestFor(email));
    }

    // --- helpers --------------------------------------------------------------

    private static bool Channel(JsonElement matrix, string category, string channelKey)
    {
        foreach (var c in matrix.GetProperty("categories").EnumerateArray())
        {
            if (c.GetProperty("category").GetString() == category)
            {
                return c.GetProperty("channels").GetProperty(channelKey).GetBoolean();
            }
        }

        throw new Xunit.Sdk.XunitException($"Category {category} not found in matrix.");
    }

    private static async Task<JsonElement> ListNotificationsAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        return page.GetProperty("items").Clone();
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
