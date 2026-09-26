using System.Net.Http.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Chat.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Who does <b>not</b> get a chat notification (feature 056).
/// </summary>
/// <remarks>
/// <para>
/// This is the safety surface of the feature, and every clause in it fails silently: drop one and
/// the symptom is a notification somebody was not supposed to receive, seen only by them. Nothing
/// else in the suite would notice.
/// </para>
/// <para>
/// Each test pairs the suppressed member with an <b>unsuppressed control in the same pass</b>, so a
/// green result cannot come from the pass having done nothing at all.
/// </para>
/// </remarks>
[Collection("Chat")]
public sealed class ChatPushEligibilityTests : ChatTestSupport
{
    public ChatPushEligibilityTests(JuggerHubApiFactory factory) : base(factory) => Factory.PushDispatcher.Clear();

    private async Task<int> RunPassAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IChatPushScanner>().RunOnceAsync();
    }

    private async Task DrainAsync()
    {
        await RunPassAsync();
        Factory.PushDispatcher.Clear();
    }

    private static async Task SetStateAsync(HttpClient client, Guid conversationId, object patch)
    {
        var resp = await client.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/state", patch);
        resp.EnsureSuccessStatusCode();
    }

    // --- Mute: the only lever that actually suppresses -------------------------

    [Fact]
    public async Task A_muted_conversation_sends_nothing_while_an_unmuted_one_still_does()
    {
        // Feature 048 made mute the product's named answer to "I want to stay on this team but I
        // don't want this chat bothering me". People are already relying on it, so a notification
        // that ignored it would break a working control rather than miss a new one.
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (cara, caraId, _) = await NewUserAsync();

        var muted = await StartDirectAsync(ada, benId);
        await SendAsync(ada, muted, "seed so the conversation exists for ben");
        await DrainAsync();
        await SetStateAsync(ben, muted, new { isMuted = true });

        var control = await StartDirectAsync(ada, caraId);

        await SendAsync(ada, muted, "you asked not to be bothered");
        await SendAsync(ada, control, "but cara did not");

        await RunPassAsync();

        Assert.DoesNotContain(benId, Factory.PushDispatcher.Recipients);
        Assert.Contains(caraId, Factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task Unmuting_resumes_notifications()
    {
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "seed");
        await DrainAsync();

        await SetStateAsync(ben, conversationId, new { isMuted = true });
        await SendAsync(ada, conversationId, "silent");
        await RunPassAsync();
        Assert.Empty(Factory.PushDispatcher.Dispatches);

        await SetStateAsync(ben, conversationId, new { isMuted = false });
        await SendAsync(ada, conversationId, "audible again");
        await RunPassAsync();

        Assert.Contains(benId, Factory.PushDispatcher.Recipients);
    }

    // --- Hide: a race, not a general suppressor --------------------------------

    [Fact]
    public async Task Archiving_during_the_quiet_delay_stops_the_notification()
    {
        // THE ONLY CASE FR-011 COVERS. A member-written message already clears IsHidden for
        // everyone through ChatMessageService.ReturnToArchiversInboxesAsync (feature 048, FR-007),
        // so by the time the pass runs the flag is false for anyone who archived BEFORE the
        // message arrived. It matches only when they archive DURING the delay — which is the
        // moment they said they did not want to hear about it.
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "seed");
        await DrainAsync();

        // The message lands, un-hiding the conversation for everyone…
        await SendAsync(ada, conversationId, "arrives while ben is tidying up");
        // …and ben archives it before the pass looks.
        await SetStateAsync(ben, conversationId, new { isHidden = true });

        await RunPassAsync();

        Assert.DoesNotContain(benId, Factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task Archiving_before_the_message_does_NOT_suppress_it()
    {
        // Deliberately asserting the surprising half, so nobody later "fixes" the clause above
        // into a general suppressor. Hide means archive — "tidy away until something happens" —
        // and a member writing to you IS something happening. Mute is the lever for silence.
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "seed");
        await DrainAsync();
        await SetStateAsync(ben, conversationId, new { isHidden = true });

        await SendAsync(ada, conversationId, "this brings it back");
        await RunPassAsync();

        Assert.Contains(benId, Factory.PushDispatcher.Recipients);
    }

    // --- Left, blocked, joined later -------------------------------------------

    [Fact]
    public async Task Someone_who_left_a_group_hears_nothing_more_from_it()
    {
        await DrainAsync();
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, caraId, _) = await NewUserAsync();

        var conversationId = await SeedConversationAsync(ConversationKind.Group, null, adaId, benId, caraId);

        var left = await ben.DeleteAsync($"/api/v1/chat/conversations/{conversationId}/members/me");
        left.EnsureSuccessStatusCode();
        await DrainAsync();

        await SendAsync(ada, conversationId, "the group carries on");
        await RunPassAsync();

        Assert.DoesNotContain(benId, Factory.PushDispatcher.Recipients);
        Assert.Contains(caraId, Factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task Blocking_someone_inside_the_quiet_delay_stops_their_message_notifying_you()
    {
        // The block clause in the pass is reachable in exactly one way, and writing this test is
        // what showed it: ChatMessageService.SendAsync ALREADY refuses a send between blocked
        // players with 403, so no message from a blocked sender can ever exist to be considered.
        // The gap it closes is the block created AFTER the message was sent and before the pass
        // looked — the same shape as archiving during the delay.
        await DrainAsync();
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (cara, caraId, _) = await NewUserAsync();

        var blocked = await StartDirectAsync(ada, benId);
        var control = await StartDirectAsync(cara, benId);
        await DrainAsync();

        await SendAsync(ada, blocked, "sent a moment before the block");
        await SendAsync(cara, control, "cara is not blocked");

        // Ben blocks Ada while her message is still inside the quiet delay.
        await BlockAsync(benId, adaId);

        await RunPassAsync();

        Assert.DoesNotContain(benId, Factory.PushDispatcher.Dispatches
            .Where(d => d.Content.Tag == $"chat:{blocked}")
            .SelectMany(d => d.RecipientUserIds));

        Assert.Contains(benId, Factory.PushDispatcher.Dispatches
            .Where(d => d.Content.Tag == $"chat:{control}")
            .SelectMany(d => d.RecipientUserIds));
    }

    [Fact]
    public async Task A_blocked_player_cannot_produce_a_message_to_notify_about_at_all()
    {
        // The first line of defence, asserted here so the pass's own block clause is understood as
        // defence in depth rather than as the only thing standing between a blocked player and
        // somebody's lock screen.
        await DrainAsync();
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await BlockAsync(benId, adaId);

        var refused = await ada.PostAsJsonAsync(
            $"/api/v1/chat/conversations/{conversationId}/messages", new { body = "let me through" });

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, refused.StatusCode);

        await RunPassAsync();
        Assert.Empty(Factory.PushDispatcher.Dispatches);
    }

    [Fact]
    public async Task A_member_who_joined_after_the_message_is_not_told_about_it()
    {
        // Being added to a team chat does not hand you the backlog from before you were there —
        // the same rule the unread badge and the inbox already apply.
        await DrainAsync();
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        var conversationId = await SeedConversationAsync(ConversationKind.Team, teamId, adaId);

        var messageId = await SendAsync(ada, conversationId, "said before ben arrived");

        // Backdate the message so it predates the join that follows, without waiting.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.ChatMessages.Where(m => m.Id == messageId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedDate, DateTime.UtcNow.AddMinutes(-5)));
        }

        await AddTeamMemberAsync(teamId, benId);

        await RunPassAsync();

        Assert.DoesNotContain(benId, Factory.PushDispatcher.Recipients);
    }

    // --- The off switch (feature 056, FR-027) ----------------------------------

    [Fact]
    public async Task Turning_chat_push_off_silences_it_while_an_untouched_member_still_hears()
    {
        // THE TRAP THIS GUARDS: IPushDispatcher filters no preferences. Without the
        // GetEnabledRecipientsAsync call in the pass, this toggle stores a value nothing consults
        // and every other test in this suite still passes.
        await DrainAsync();
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var (_, caraId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);
        await AddTeamMemberAsync(teamId, caraId);
        var conversationId = await SeedConversationAsync(ConversationKind.Team, teamId, adaId);

        var off = await ben.PutAsJsonAsync(
            "/api/v1/notification-preferences/Chat/Push", new { enabled = false });
        off.EnsureSuccessStatusCode();

        await SendAsync(ada, conversationId, "training moved");
        await RunPassAsync();

        Assert.DoesNotContain(benId, Factory.PushDispatcher.Recipients);
        Assert.Contains(caraId, Factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task Chat_and_the_other_categories_do_not_affect_each_other()
    {
        await DrainAsync();
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();

        // Ben wants nothing from team news on his phone, but has said nothing about chat.
        var off = await ben.PutAsJsonAsync(
            "/api/v1/notification-preferences/TeamNews/Push", new { enabled = false });
        off.EnsureSuccessStatusCode();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "unrelated to team news");
        await RunPassAsync();

        Assert.Contains(benId, Factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task A_member_who_never_touched_the_setting_is_notified()
    {
        // No stored row means on, the same rule every other category follows.
        await DrainAsync();
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        await SendAsync(ada, conversationId, "default is on");
        await RunPassAsync();

        Assert.Contains(benId, Factory.PushDispatcher.Recipients);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == benId && p.Category == NotificationCategory.Chat)
            .ToListAsync());
    }
}
