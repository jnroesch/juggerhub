using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Push;

/// <summary>
/// <b>The regression guard for feature 055.</b>
///
/// <para>
/// The in-app preference check in <c>NotificationService</c> used to be an early <c>return</c> that
/// gated everything after it. A push dispatch added below that return would have been silently
/// switched off by an unrelated toggle — a member who wanted team news on their phone but not in
/// their Alerts inbox would have received neither, with nothing to explain why.
/// </para>
///
/// <para>
/// Three of the four combinations below are new behaviour. If exactly one test in this feature is
/// trusted, it is this one.
/// </para>
/// </summary>
[Collection("Teams")]
public sealed class NotificationChannelIndependenceTests
{
    private readonly JuggerHubApiFactory _factory;

    public NotificationChannelIndependenceTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task In_app_on_push_on_writes_a_row_and_dispatches()
    {
        var (admin, slug) = await NewTeamAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        _factory.PushDispatcher.Clear();

        await InviteAsync(admin, slug, targetId);

        Assert.NotEmpty((await ListNotificationsAsync(target)).EnumerateArray());
        Assert.Contains(targetId, _factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task In_app_on_push_off_writes_a_row_and_does_not_dispatch()
    {
        var (admin, slug) = await NewTeamAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        await SetAsync(target, "InvitesAndRoster", "Push", false);
        _factory.PushDispatcher.Clear();

        await InviteAsync(admin, slug, targetId);

        Assert.NotEmpty((await ListNotificationsAsync(target)).EnumerateArray());
        Assert.DoesNotContain(targetId, _factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task In_app_off_push_on_still_dispatches()
    {
        // THE ONE THAT WOULD HAVE REGRESSED SILENTLY. Turning the Alerts inbox off for a category
        // must not stop that category reaching the member's phone.
        var (admin, slug) = await NewTeamAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        await SetAsync(target, "InvitesAndRoster", "InApp", false);
        _factory.PushDispatcher.Clear();

        await InviteAsync(admin, slug, targetId);

        Assert.Empty((await ListNotificationsAsync(target)).EnumerateArray());
        Assert.Contains(targetId, _factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task Both_off_does_neither()
    {
        var (admin, slug) = await NewTeamAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        await SetAsync(target, "InvitesAndRoster", "InApp", false);
        await SetAsync(target, "InvitesAndRoster", "Push", false);
        _factory.PushDispatcher.Clear();

        await InviteAsync(admin, slug, targetId);

        Assert.Empty((await ListNotificationsAsync(target)).EnumerateArray());
        Assert.DoesNotContain(targetId, _factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task A_dispatch_failure_leaves_the_producing_action_successful()
    {
        // FR-013: a push that cannot leave the building must not fail the invitation that caused it.
        var (admin, slug) = await NewTeamAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        _factory.PushDispatcher.Clear();
        _factory.PushDispatcher.ThrowOnDispatch = true;

        try
        {
            var invite = await InviteAsync(admin, slug, targetId);
            invite.EnsureSuccessStatusCode();

            // The in-app half is untouched by the push failure.
            Assert.NotEmpty((await ListNotificationsAsync(target)).EnumerateArray());
        }
        finally
        {
            _factory.PushDispatcher.ThrowOnDispatch = false;
        }
    }

    [Fact]
    public async Task The_notification_is_composed_in_the_recipients_language()
    {
        // The actor's language is irrelevant to the recipient (FR-008).
        var (admin, slug) = await NewTeamAsync();
        var (target, targetId, _, _) = await NewUserAsync();
        await target.PutAsJsonAsync("/api/v1/account/language", new { language = "de" });
        _factory.PushDispatcher.Clear();

        await InviteAsync(admin, slug, targetId);

        var dispatch = Assert.Single(
            _factory.PushDispatcher.Dispatches,
            d => d.RecipientUserIds.Contains(targetId));
        Assert.Contains("eingeladen", dispatch.Content.Body);
        Assert.StartsWith("/t/", dispatch.Content.Url);
    }

    // --- helpers ------------------------------------------------------------

    private static Task<HttpResponseMessage> SetAsync(
        HttpClient client, string category, string channel, bool enabled) =>
        client.PutAsJsonAsync($"/api/v1/notification-preferences/{category}/{channel}", new { enabled });

    private static Task<HttpResponseMessage> InviteAsync(HttpClient admin, string slug, Guid targetId) =>
        admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });

    private static async Task<JsonElement> ListNotificationsAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        return page.GetProperty("items");
    }

    private async Task<(HttpClient Admin, string Slug)> NewTeamAsync()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var created = await admin.PostAsJsonAsync("/api/v1/teams", new
        {
            name = "Rheinfeuer",
            slug,
            type = "CityTeam",
            location = new { cityExternalId = "TEST:berlin" },
        });
        created.EnsureSuccessStatusCode();
        return (admin, slug);
    }

    private async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }
}
