using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Withdrawing a message (feature 019, FR-050 / SC-013) and the deleted/banned-sender placeholder.
/// </summary>
[Collection("Chat")]
public sealed class ChatDeleteTests : ChatTestSupport
{
    public ChatDeleteTests(JuggerHubApiFactory factory) : base(factory) { }

    /// <summary>FR-050: the content goes for everyone, and a tombstone keeps its place in the thread.</summary>
    [Fact]
    public async Task A_sender_can_delete_their_own_message_and_it_vanishes_for_both()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, "before");
        var target = await SendAsync(ada, conversationId, "oops wrong thread");
        await SendAsync(ada, conversationId, "after");

        var resp = await ada.DeleteAsync($"/api/v1/chat/messages/{target}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        foreach (var viewer in new[] { ada, ben })
        {
            var page = await GetMessagesAsync(viewer, conversationId);
            var items = page.GetProperty("items").EnumerateArray().ToList();

            // Still three messages: the tombstone holds its ordinal position rather than the thread
            // silently re-flowing around a hole.
            Assert.Equal(3, items.Count);

            var tombstone = items.Single(m => m.GetProperty("id").GetGuid() == target);
            Assert.True(tombstone.GetProperty("isDeleted").GetBoolean());
            Assert.Equal(string.Empty, tombstone.GetProperty("body").GetString());

            // The neighbours are untouched.
            Assert.Equal("after", items[0].GetProperty("body").GetString());
            Assert.Equal("before", items[2].GetProperty("body").GetString());
        }
    }

    /// <summary>
    /// The body is genuinely cleared in the database, not merely flagged. A flag alone would leave the
    /// text sitting in the row for any future query that forgot to check it.
    /// </summary>
    [Fact]
    public async Task Deleting_clears_the_body_in_the_database_not_just_a_flag()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var target = await SendAsync(ada, conversationId, "sensitive thing");
        (await ada.DeleteAsync($"/api/v1/chat/messages/{target}")).EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ChatMessages.AsNoTracking().FirstAsync(m => m.Id == target);

        Assert.True(row.IsDeleted);
        Assert.Equal(string.Empty, row.Body);
        Assert.Equal(ChatLinkKind.None, row.LinkKind);
        Assert.Null(row.LinkTargetId);
    }

    /// <summary>FR-050a: a member of the conversation who did not write it gets 403 — not 404, since they can see it.</summary>
    [Fact]
    public async Task A_member_cannot_delete_someone_elses_message()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var target = await SendAsync(ada, conversationId, "mine");

        var resp = await ben.DeleteAsync($"/api/v1/chat/messages/{target}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        // And it really is still there.
        var page = await GetMessagesAsync(ben, conversationId);
        Assert.False(page.GetProperty("items")[0].GetProperty("isDeleted").GetBoolean());
    }

    /// <summary>A non-member gets 404 — they must not learn that the message id is real.</summary>
    [Fact]
    public async Task A_non_member_deleting_gets_404()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (mallory, _, _) = await NewUserAsync();

        var conversationId = await StartDirectAsync(ada, benId);
        var target = await SendAsync(ada, conversationId, "mine");

        var resp = await mallory.DeleteAsync($"/api/v1/chat/messages/{target}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    /// <summary>FR-050c: the inbox stops showing content the sender withdrew.</summary>
    [Fact]
    public async Task A_deleted_last_message_drops_out_of_the_inbox_preview()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var target = await SendAsync(ada, conversationId, "regrettable");
        (await ada.DeleteAsync($"/api/v1/chat/messages/{target}")).EnsureSuccessStatusCode();

        var inbox = await GetInboxAsync(ada);
        var row = inbox.GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conversationId);

        Assert.Equal(string.Empty, row.GetProperty("lastMessage").GetProperty("preview").GetString());
    }

    /// <summary>Deleting twice is a no-op, not an error.</summary>
    [Fact]
    public async Task Deleting_an_already_deleted_message_is_idempotent()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var target = await SendAsync(ada, conversationId, "gone");

        Assert.Equal(HttpStatusCode.NoContent, (await ada.DeleteAsync($"/api/v1/chat/messages/{target}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ada.DeleteAsync($"/api/v1/chat/messages/{target}")).StatusCode);
    }

    /// <summary>
    /// FR-050b: a sent message is immutable — the only correction is delete-and-resend. This pins the
    /// absence of an edit route, so adding one becomes a deliberate spec change rather than a drive-by.
    /// </summary>
    [Fact]
    public async Task There_is_no_edit_route_for_a_message()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);
        var target = await SendAsync(ada, conversationId, "typo");

        var patch = await ada.PatchAsJsonAsync($"/api/v1/chat/messages/{target}", new { body = "fixed" });

        Assert.True(
            patch.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"expected no edit route, got {(int)patch.StatusCode}");
    }

    /// <summary>
    /// A banned account's profile is hidden by a global query filter (feature 013), so a naive
    /// projection would return a null sender name — or blow up. Their past messages must still read
    /// coherently for everyone else in the conversation: history is preserved, not rewritten.
    /// </summary>
    [Fact]
    public async Task A_banned_senders_messages_still_render_with_a_placeholder_identity()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, "see you at training");

        // Ada's account is banned (feature 013 soft-delete semantics).
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // User is an IdentityUser, not a BaseEntity — no audit columns to maintain here.
            await db.Users.Where(u => u.Id == adaId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, AccountStatus.Banned));
        }

        var page = await GetMessagesAsync(ben, conversationId);
        var message = page.GetProperty("items")[0];

        // The message survives, with its text, under a neutral identity.
        Assert.Equal("see you at training", message.GetProperty("body").GetString());
        Assert.False(message.GetProperty("body").GetString()!.Length == 0);
        Assert.Equal("A former player", message.GetProperty("senderName").GetString());
    }
}
