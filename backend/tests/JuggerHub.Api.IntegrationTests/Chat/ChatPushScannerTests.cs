using System.Net.Http.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Chat.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// The chat push pass: what it picks up, what it collapses, and what it refuses to send twice
/// (feature 056 / GH #309).
/// </summary>
/// <remarks>
/// <para>
/// Every test drives <b>one deterministic pass</b> through <see cref="IChatPushScanner"/> rather
/// than waiting on the hosted service's timer, which is off in tests. The quiet delay is
/// configured to zero there too, so eligibility is decided purely by the state each test sets up —
/// which is the thing under test.
/// </para>
/// <para>
/// The dispatcher is <see cref="Push.FakePushDispatcher"/>, so nothing leaves the test host and a
/// test can assert on who was <em>not</em> sent to, which is where this feature's real risk lives.
/// </para>
/// </remarks>
[Collection("Chat")]
public sealed class ChatPushScannerTests : ChatTestSupport
{
    public ChatPushScannerTests(JuggerHubApiFactory factory) : base(factory) => Factory.PushDispatcher.Clear();

    private async Task<int> RunPassAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IChatPushScanner>().RunOnceAsync();
    }

    /// <summary>Marks everything already in flight as considered, so a test starts from a clean slate.</summary>
    private async Task DrainAsync()
    {
        await RunPassAsync();
        Factory.PushDispatcher.Clear();
    }

    // --- The happy path -------------------------------------------------------

    [Fact]
    public async Task An_unread_direct_message_reaches_the_recipient()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        await SetDisplayNameAsync(benId, "Ben Ohne");

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "Are we training tomorrow?");

        await RunPassAsync();

        var dispatch = Assert.Single(Factory.PushDispatcher.Dispatches);
        Assert.Equal([benId], dispatch.RecipientUserIds);
        Assert.Equal("Are we training tomorrow?", dispatch.Content.Body);
        Assert.Equal($"/chat/{conversationId}", dispatch.Content.Url);
        Assert.Equal($"chat:{conversationId}", dispatch.Content.Tag);
    }

    [Fact]
    public async Task The_sender_is_never_told_about_their_own_message()
    {
        await DrainAsync();
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "hello");

        await RunPassAsync();

        Assert.DoesNotContain(adaId, Factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task A_message_already_read_produces_nothing()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        var messageId = await SendAsync(ada, conversationId, "you are already looking at this");
        await MarkReadAsync(ben, conversationId, messageId);

        await RunPassAsync();

        Assert.DoesNotContain(benId, Factory.PushDispatcher.Recipients);
    }

    // --- Once only, and collapsed ---------------------------------------------

    [Fact]
    public async Task A_message_is_considered_once_however_often_the_pass_runs()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "only once");

        await RunPassAsync();
        var afterFirst = Factory.PushDispatcher.Dispatches.Count;
        await RunPassAsync();
        await RunPassAsync();

        Assert.Equal(1, afterFirst);
        Assert.Equal(afterFirst, Factory.PushDispatcher.Dispatches.Count);
    }

    [Fact]
    public async Task Four_messages_in_one_conversation_become_one_notification()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "one");
        await SendAsync(ada, conversationId, "two");
        await SendAsync(ada, conversationId, "three");
        await SendAsync(ada, conversationId, "four");

        await RunPassAsync();

        var dispatch = Assert.Single(Factory.PushDispatcher.Dispatches);
        // The NEWEST unread message, not the oldest: it is the most recent thing they missed.
        Assert.Equal("four", dispatch.Content.Body);
        Assert.Equal($"chat:{conversationId}", dispatch.Content.Tag);
    }

    [Fact]
    public async Task Two_conversations_produce_two_notifications_with_different_tags()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (cara, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var first = await StartDirectAsync(ada, benId);
        var second = await StartDirectAsync(cara, benId);
        await SendAsync(ada, first, "from ada");
        await SendAsync(cara, second, "from cara");

        await RunPassAsync();

        Assert.Equal(2, Factory.PushDispatcher.Dispatches.Count);
        Assert.Equal(2, Factory.PushDispatcher.Dispatches.Select(d => d.Content.Tag).Distinct().Count());
    }

    // --- What never notifies anybody ------------------------------------------

    [Fact]
    public async Task A_system_line_notifies_nobody()
    {
        await DrainAsync();
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        var conversationId = await SeedConversationAsync(ConversationKind.Team, teamId, adaId);
        await AddTeamMemberAsync(teamId, benId);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ChatMessages.Add(new ChatMessage
            {
                ConversationId = conversationId,
                Kind = ChatMessageKind.System,
                SystemEvent = ChatSystemEvent.Joined,
                SystemSubjectUserId = benId,
                BodyCipher = [],
            });
            await db.SaveChangesAsync();
        }

        await RunPassAsync();

        // Excluded by the SELECT, not by a later branch — which is what makes it structural.
        Assert.Empty(Factory.PushDispatcher.Dispatches);
    }

    [Fact]
    public async Task A_message_deleted_before_the_pass_notifies_nobody()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        var messageId = await SendAsync(ada, conversationId, "withdrawn");
        var deleted = await ada.DeleteAsync($"/api/v1/chat/messages/{messageId}");
        deleted.EnsureSuccessStatusCode();

        await RunPassAsync();

        Assert.Empty(Factory.PushDispatcher.Dispatches);
    }

    [Fact]
    public async Task A_message_older_than_the_maximum_age_is_marked_but_never_sent()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        var messageId = await SendAsync(ada, conversationId, "yesterday's news");

        // Backdate it past MaxMessageAgeMinutes. A restored service must not buzz every phone on
        // the platform about a conversation that has moved on.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.ChatMessages.Where(m => m.Id == messageId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedDate, DateTime.UtcNow.AddDays(-1)));
        }

        await RunPassAsync();

        Assert.Empty(Factory.PushDispatcher.Dispatches);

        // …and it is MARKED anyway. This is the index invariant: a selected-but-unmarked row sits
        // in the partial index for ever, and the index being small is the only reason this query
        // is affordable every few seconds.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var considered = await db.ChatMessages.AsNoTracking()
                .Where(m => m.Id == messageId)
                .Select(m => m.PushConsideredAt)
                .FirstAsync();

            Assert.NotNull(considered);
        }
    }

    // --- The decision this whole design protects ------------------------------

    [Fact]
    public async Task Nothing_in_the_pass_writes_a_notification_row()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "still not an alert");

        await RunPassAsync();

        // Chat reaches a device through IPushDispatcher directly, below the notification store.
        // If this ever fails, chat has been wired into the Alerts spine — the thing feature 019
        // refused and feature 055 shaped its architecture to keep possible to refuse.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == benId).ToListAsync());
    }

    // --- Failure is contained --------------------------------------------------

    [Fact]
    public async Task A_failing_dispatcher_leaves_the_message_and_the_pass_intact()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "the push will fail");

        Factory.PushDispatcher.ThrowOnDispatch = true;
        try
        {
            // The pass must not throw at its caller: the hosted service logs and carries on.
            var considered = await RunPassAsync();
            Assert.True(considered > 0);
        }
        finally
        {
            Factory.PushDispatcher.ThrowOnDispatch = false;
        }

        // The message is still there, still readable, and still counted as unread for its
        // recipient — a failed notification is a missed convenience, never lost data.
        var messages = await GetMessagesAsync(ada, conversationId);
        Assert.Contains("the push will fail", messages.ToString());
        Assert.Equal(1, await GetUnreadTotalAsync(ben));
    }

    [Fact]
    public async Task Sending_still_works_while_dispatch_is_failing()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        Factory.PushDispatcher.ThrowOnDispatch = true;
        try
        {
            await RunPassAsync();
            // Nothing about chat depends on the pass. A member who could send a message a minute
            // ago can still send one.
            var messageId = await SendAsync(ada, conversationId, "chat is unaffected");
            Assert.NotEqual(Guid.Empty, messageId);
        }
        finally
        {
            Factory.PushDispatcher.ThrowOnDispatch = false;
        }
    }

    [Fact]
    public void The_pass_runs_from_a_hosted_service()
    {
        // The factory disables the timer, so every other test here drives the pass by hand and
        // none of them would notice if the background service were dropped from Program.cs — chat
        // push would simply stop happening in Prod, silently, with a full green suite.
        Assert.Contains(
            Factory.Services.GetServices<IHostedService>(),
            s => s is JuggerHub.Services.Hosted.ChatPushBackgroundService);
    }

    // --- Team conversations ----------------------------------------------------

    [Fact]
    public async Task A_team_message_reaches_every_other_member_once()
    {
        await DrainAsync();
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, caraId, _) = await NewUserAsync();
        await SetDisplayNameAsync(adaId, "Ada Kuss");

        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);
        await AddTeamMemberAsync(teamId, caraId);
        var conversationId = await SeedConversationAsync(ConversationKind.Team, teamId, adaId);

        await SendAsync(ada, conversationId, "training moved to 19:00");
        await RunPassAsync();

        var recipients = Factory.PushDispatcher.Recipients;
        Assert.Contains(benId, recipients);
        Assert.Contains(caraId, recipients);
        Assert.DoesNotContain(adaId, recipients);

        // The title is the chat, and the sender moves into the body, so a member of two team
        // chats can tell which one is talking.
        var dispatch = Factory.PushDispatcher.Dispatches.First();
        Assert.Contains("Ada Kuss", dispatch.Content.Body);
        Assert.Contains("training moved to 19:00", dispatch.Content.Body);
    }
}
