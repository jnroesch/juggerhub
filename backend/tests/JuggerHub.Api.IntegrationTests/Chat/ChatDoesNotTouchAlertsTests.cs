using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Chat keeps its own inbox and its own badge, and stays out of the Alerts spine (feature 019,
/// FR-051 / FR-051a / SC-014).
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>negative</b> requirement, decided in the 2026-07-16 clarification session: a chat is
/// already an inbox, so mirroring every message into Alerts would be duplicate noise that buries the
/// team invites and training notices Alerts exists for.
/// </para>
/// <para>
/// Nothing else in the suite would catch a violation — there is no feature to build here, only a
/// discipline to keep — so this file is the regression guard. If someone later wires chat into the
/// notification store, these tests fail and force the conversation rather than letting it happen
/// quietly.
/// </para>
/// </remarks>
[Collection("Chat")]
public sealed class ChatDoesNotTouchAlertsTests : ChatTestSupport
{
    public ChatDoesNotTouchAlertsTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task An_unread_chat_message_leaves_the_alerts_unread_count_unchanged()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var before = await GetAlertsUnreadAsync(ben);

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "this must not ring the Alerts bell");
        await SendAsync(ada, conversationId, "nor this");

        // Chat's own badge moved…
        Assert.Equal(2, await GetUnreadTotalAsync(ben));

        // …and Alerts did not.
        Assert.Equal(before, await GetAlertsUnreadAsync(ben));
    }

    [Fact]
    public async Task Sending_a_chat_message_creates_no_notification_row()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var notifications = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == benId)
            .ToListAsync();

        Assert.Empty(notifications);
    }

    /// <summary>
    /// FR-051a, first half: chat introduces no new notification <b>type</b>. Pinning the enum
    /// members means adding a chat one becomes a deliberate, reviewed change to feature 010/011's
    /// contract — not a side effect of a chat PR.
    /// </summary>
    /// <remarks>
    /// <b>This test worked.</b> It failed when feature 056 added <c>NotificationCategory.Chat</c>,
    /// which forced exactly the conversation it was written to force, and the owner decided that
    /// chat should be refusable in the same list as everything else. The category half of FR-051a
    /// is therefore amended and lives in
    /// <see cref="Chat_has_a_preference_category_but_still_no_producer_type"/> below; the type half
    /// is unchanged and still guarded here.
    /// </remarks>
    [Fact]
    public void Chat_adds_no_notification_type()
    {
        var types = Enum.GetNames<NotificationType>();
        Assert.DoesNotContain(types, t => t.Contains("Chat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(types, t => t.Contains("Message", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// FR-051a as amended by feature 056: chat has a preference category, and it is Push-only with
    /// no producer type behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The category exists for one reason — a member must be able to say "not chat, on my phone" in
    /// the place they say it about everything else. It does not put chat into the Alerts spine, and
    /// the two tests above are what keep that true.
    /// </para>
    /// <para>
    /// The In-app and E-mail assertions are the load-bearing ones here. If chat ever became
    /// deliverable in-app, it would mean rows in the Alerts inbox — the thing feature 019 refused
    /// and this file exists to prevent — and it would happen through the preference matrix rather
    /// than through a chat PR, where nobody would be looking for it.
    /// </para>
    /// </remarks>
    [Fact]
    public void Chat_has_a_preference_category_but_still_no_producer_type()
    {
        Assert.Contains(Enum.GetNames<NotificationCategory>(), c => c == nameof(NotificationCategory.Chat));

        Assert.DoesNotContain(
            Enum.GetValues<NotificationType>().Select(NotificationCategories.For),
            c => c == NotificationCategory.Chat);

        Assert.True(NotificationCategories.Supports(NotificationCategory.Chat, NotificationChannel.Push));
        Assert.False(NotificationCategories.Supports(NotificationCategory.Chat, NotificationChannel.InApp));
        Assert.False(NotificationCategories.Supports(NotificationCategory.Chat, NotificationChannel.Email));
    }

    private static async Task<int> GetAlertsUnreadAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/v1/notifications/unread-count");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);

        // The notifications contract exposes the count under "unreadCount".
        return body.TryGetProperty("unreadCount", out var v) ? v.GetInt32() : 0;
    }
}
