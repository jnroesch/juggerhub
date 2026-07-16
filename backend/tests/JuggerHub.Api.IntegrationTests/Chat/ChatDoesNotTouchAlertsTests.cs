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
    /// FR-051a: chat introduces no new notification type or preference category. Pinning the enum
    /// members means adding a chat one becomes a deliberate, reviewed change to feature 010/011's
    /// contract — not a side effect of a chat PR.
    /// </summary>
    [Fact]
    public void Chat_adds_no_notification_type_or_category()
    {
        var types = Enum.GetNames<NotificationType>();
        Assert.DoesNotContain(types, t => t.Contains("Chat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(types, t => t.Contains("Message", StringComparison.OrdinalIgnoreCase));

        var categories = Enum.GetNames<NotificationCategory>();
        Assert.DoesNotContain(categories, c => c.Contains("Chat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(categories, c => c.Contains("Message", StringComparison.OrdinalIgnoreCase));
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
