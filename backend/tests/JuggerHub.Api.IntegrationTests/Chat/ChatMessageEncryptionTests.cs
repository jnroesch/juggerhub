using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Chat;
using JuggerHub.Services.Chat.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Chat message encryption at rest, end to end through the real API and a real Postgres
/// (feature 047 / #223).
/// </summary>
/// <remarks>
/// The point of these, as opposed to <see cref="ChatMessageCipherTests"/>, is that they read the
/// <em>stored row</em>. A cipher that round-trips in isolation proves nothing about whether the
/// send path actually used it.
/// </remarks>
[Collection("Chat")]
public sealed class ChatMessageEncryptionTests(JuggerHubApiFactory factory) : ChatTestSupport(factory)
{
    private async Task<ChatMessage> RowAsync(Guid messageId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ChatMessages.AsNoTracking().FirstAsync(m => m.Id == messageId);
    }

    // --- SC-001: the row does not contain what was typed ------------------------

    [Fact]
    public async Task What_a_player_typed_is_absent_from_the_stored_row()
    {
        const string text = "meet at the north pitch at 18:00";
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var messageId = await SendAsync(ada, conversationId, text);
        var row = await RowAsync(messageId);

        var asBytes = Encoding.Latin1.GetString(row.BodyCipher);
        Assert.DoesNotContain(text, asBytes, StringComparison.Ordinal);
        Assert.DoesNotContain("pitch", asBytes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("18:00", asBytes, StringComparison.Ordinal);

        // 29-byte envelope + the UTF-8 length of the text.
        Assert.Equal(29 + Encoding.UTF8.GetByteCount(text), row.BodyCipher.Length);
        Assert.Equal(1, row.BodyCipher[0]); // the key version the test host is configured with
    }

    [Fact]
    public async Task The_stored_row_decrypts_only_when_bound_to_its_own_id()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var messageId = await SendAsync(ada, conversationId, "bound to this row");
        var row = await RowAsync(messageId);

        Assert.True(ChatMessageSeed.TryUnprotect(row.BodyCipher, messageId, out var text));
        Assert.Equal("bound to this row", text);

        // The associated-data binding: an operator with write access cannot move this ciphertext
        // onto another row and have it read as that person's words.
        Assert.False(ChatMessageSeed.TryUnprotect(row.BodyCipher, Guid.CreateVersion7(), out _));
    }

    // --- SC-002: nothing a player sees has changed ------------------------------

    [Theory]
    [InlineData("plain ascii")]
    [InlineData("emoji \U0001F3C6 and \U0001F94A")]
    [InlineData("line one\nline two")]
    [InlineData("umlauts äöü and ß")]
    public async Task A_message_reads_back_exactly_as_typed(string text)
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, text);

        var page = await GetMessagesAsync(ben, conversationId);
        var body = page.GetProperty("items")[0].GetProperty("body").GetString();
        Assert.Equal(text, body);
    }

    [Fact]
    public async Task A_message_at_the_length_limit_round_trips_and_one_over_is_still_refused()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var atLimit = new string('x', ChatConstants.MaxMessageLength);
        var messageId = await SendAsync(ada, conversationId, atLimit);
        var page = await GetMessagesAsync(ada, conversationId);
        Assert.Equal(atLimit, page.GetProperty("items")[0].GetProperty("body").GetString());
        Assert.Equal(29 + ChatConstants.MaxMessageLength, (await RowAsync(messageId)).BodyCipher.Length);

        // The limit is still a check on the text the player typed, not on the stored size.
        var tooLong = await ada.PostAsJsonAsync(
            $"/api/v1/chat/conversations/{conversationId}/messages",
            new { body = new string('x', ChatConstants.MaxMessageLength + 1) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task The_inbox_preview_shows_the_real_text()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, "bring the chain");

        var inbox = await ben.GetFromJsonAsync<JsonElement>("/api/v1/chat/conversations", Json);
        var row = inbox.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("id").GetGuid() == conversationId);
        Assert.Equal("bring the chain", row.GetProperty("lastMessage").GetProperty("preview").GetString());
    }

    [Fact]
    public async Task A_link_still_resolves_its_card()
    {
        // Unfurl parses the text the player typed, before encryption — the only moment it exists
        // in readable form (FR-012).
        var (ada, adaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);
        var conversationId = await StartDirectAsync(ada, benId);

        await SendAsync(ada, conversationId, $"have a look: http://localhost:3000/t/{slug}");

        var page = await GetMessagesAsync(ada, conversationId);
        var message = page.GetProperty("items")[0];
        Assert.True(message.TryGetProperty("linkCard", out var card) && card.ValueKind != JsonValueKind.Null,
            "the link card should still resolve when the body is encrypted");
        Assert.Equal("Rheinfeuer", card.GetProperty("title").GetString());
        _ = adaId;
    }

    // --- Deletion and system lines store nothing (data-model D2) ----------------

    [Fact]
    public async Task A_deleted_message_leaves_a_zero_length_row_and_an_empty_preview()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var messageId = await SendAsync(ada, conversationId, "withdrawn");
        (await ada.DeleteAsync($"/api/v1/chat/messages/{messageId}")).EnsureSuccessStatusCode();

        // Not an envelope around "" — a 29-byte value would be indistinguishable from a short
        // message, and the row would no longer visibly hold nothing.
        Assert.Empty((await RowAsync(messageId)).BodyCipher);

        var inbox = await ben.GetFromJsonAsync<JsonElement>("/api/v1/chat/conversations", Json);
        var row = inbox.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("id").GetGuid() == conversationId);
        Assert.Equal(string.Empty, row.GetProperty("lastMessage").GetProperty("preview").GetString());
    }

    [Fact]
    public async Task A_system_line_stores_nothing()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, benId);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conversation = new Conversation { Kind = ConversationKind.Team, TeamId = teamId };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        var messages = scope.ServiceProvider.GetRequiredService<JuggerHub.Services.Chat.IChatMessageService>();
        await messages.WriteSystemMessageAsync(conversation.Id, ChatSystemEvent.Joined, benId);

        var row = await db.ChatMessages.AsNoTracking()
            .FirstAsync(m => m.ConversationId == conversation.Id && m.Kind == ChatMessageKind.System);
        Assert.Empty(row.BodyCipher);
    }

    // --- SC-007 / FR-006: several key versions ---------------------------------

    [Fact]
    public void A_row_written_under_an_older_version_still_reads_after_a_new_key_is_prepended()
    {
        var id = Guid.CreateVersion7();
        var v1 = new AesGcmChatMessageCipher(
            new ChatEncryptionOptions { Keys = JuggerHubApiFactory.TestEncryptionKey }.Parse());
        var stored = v1.Protect("written before the rotation", id);

        var rotated = new AesGcmChatMessageCipher(new ChatEncryptionOptions
        {
            Keys = "2:/v37+vn49/b18/Lx8O/u7ezr6uno5+bl5OPi4eDf3t0=;" + JuggerHubApiFactory.TestEncryptionKey,
        }.Parse());

        Assert.True(rotated.TryUnprotect(stored, id, out var text));
        Assert.Equal("written before the rotation", text);
        Assert.Equal(2, rotated.Protect("written after", id)[0]);
    }

    // --- SC-006 / FR-009: one unreadable row does not take the thread down ------

    [Fact]
    public async Task A_corrupted_row_renders_as_unavailable_and_the_rest_of_the_thread_is_fine()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var first = await SendAsync(ada, conversationId, "first message");
        var damaged = await SendAsync(ada, conversationId, "this one gets corrupted");
        var last = await SendAsync(ada, conversationId, "third message");

        await CorruptAsync(damaged);

        var page = await GetMessagesAsync(ben, conversationId);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);

        var broken = items.Single(m => m.GetProperty("id").GetGuid() == damaged);
        Assert.True(broken.GetProperty("isUnavailable").GetBoolean());
        Assert.False(broken.GetProperty("isDeleted").GetBoolean());
        Assert.Equal(string.Empty, broken.GetProperty("body").GetString());

        // Everything else is untouched — that is the whole requirement.
        Assert.Equal("first message", items.Single(m => m.GetProperty("id").GetGuid() == first).GetProperty("body").GetString());
        Assert.Equal("third message", items.Single(m => m.GetProperty("id").GetGuid() == last).GetProperty("body").GetString());
        Assert.All(items.Where(m => m.GetProperty("id").GetGuid() != damaged),
            m => Assert.False(m.GetProperty("isUnavailable").GetBoolean()));
    }

    [Fact]
    public async Task A_corrupted_last_message_previews_as_empty_rather_than_breaking_the_inbox()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await CorruptAsync(await SendAsync(ada, conversationId, "the newest message"));

        var inbox = await ben.GetFromJsonAsync<JsonElement>("/api/v1/chat/conversations", Json);
        var row = inbox.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("id").GetGuid() == conversationId);
        Assert.Equal(string.Empty, row.GetProperty("lastMessage").GetProperty("preview").GetString());
    }

    // --- SC-009: nothing leaks into the logs -----------------------------------

    [Fact]
    public async Task Reading_a_corrupted_message_logs_no_plaintext_ciphertext_or_key()
    {
        const string text = "the sensitive part of this message";
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var messageId = await SendAsync(ada, conversationId, text);
        var original = (await RowAsync(messageId)).BodyCipher;
        await CorruptAsync(messageId);

        Factory.ErrorLogs.Clear();
        await GetMessagesAsync(ben, conversationId);

        var keyMaterial = JuggerHubApiFactory.TestEncryptionKey.Split(':')[1];
        foreach (var line in Factory.ErrorLogs)
        {
            Assert.DoesNotContain(text, line, StringComparison.Ordinal);
            Assert.DoesNotContain("sensitive", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(keyMaterial, line, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToBase64String(original), line, StringComparison.Ordinal);
        }
    }

    /// <summary>Flips a byte of a stored ciphertext, the way a corrupted row or a retired key looks.</summary>
    private async Task CorruptAsync(Guid messageId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.ChatMessages.FirstAsync(m => m.Id == messageId);
        var damaged = (byte[])message.BodyCipher.Clone();
        damaged[^1] ^= 0xFF;
        message.BodyCipher = damaged;
        await db.SaveChangesAsync();
    }
}
