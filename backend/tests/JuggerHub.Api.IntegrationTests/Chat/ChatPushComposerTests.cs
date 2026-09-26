using JuggerHub.Entities;
using JuggerHub.Services.Chat.Encryption;
using JuggerHub.Services.Chat.Push;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// What a chat notification actually says on a lock screen, and where tapping it goes
/// (feature 056). Resolved from the real container so the composer, the localizer and the cipher
/// under test are the ones the app uses.
/// </summary>
/// <remarks>
/// These are the tests for the part of this feature that leaves the platform. The payload carries
/// member-written text by owner decision, so the branches that decide <em>not</em> to include it —
/// an unreadable body, a message made only of attachments — matter as much as the happy path.
/// </remarks>
[Collection("Chat")]
public sealed class ChatPushComposerTests
{
    private readonly IChatPushComposer _composer;
    private readonly IChatMessageCipher _cipher;

    public ChatPushComposerTests(JuggerHubApiFactory factory)
    {
        var scope = factory.Services.CreateScope();
        _composer = scope.ServiceProvider.GetRequiredService<IChatPushComposer>();
        _cipher = scope.ServiceProvider.GetRequiredService<IChatMessageCipher>();
    }

    private static ChatPushConversation Direct() =>
        new(Guid.CreateVersion7(), ConversationKind.Direct, null, null, null, null, null);

    private static ChatPushConversation Team(string name = "Hamburg Hammers") =>
        new(Guid.CreateVersion7(), ConversationKind.Team, null, name, null, null, null);

    private ChatPushMessage Message(string text, string? sender = "Anna Meyer", bool attachments = false)
    {
        var id = Guid.CreateVersion7();
        return new ChatPushMessage(id, sender, text.Length == 0 ? [] : _cipher.Protect(text, id), attachments);
    }

    // --- The happy path -------------------------------------------------------

    [Fact]
    public void A_direct_message_is_headed_by_the_sender_and_shows_what_they_wrote()
    {
        var content = _composer.Compose(Direct(), Message("Are we training tomorrow?"), "en", false);

        Assert.Equal("Anna Meyer", content.Title);
        Assert.Equal("Are we training tomorrow?", content.Body);
    }

    [Fact]
    public void A_team_message_is_headed_by_the_chat_and_names_the_sender_before_the_text()
    {
        // A member of two team chats has to be able to tell which one is talking, which is why the
        // title is the conversation and the sender moves into the body (FR-018).
        var content = _composer.Compose(Team(), Message("who's bringing the pompfen?"), "en", false);

        Assert.Equal("Hamburg Hammers", content.Title);
        Assert.Equal("Anna Meyer: who's bringing the pompfen?", content.Body);
    }

    [Fact]
    public void It_opens_the_conversation_and_collapses_per_conversation()
    {
        var conversation = Team();

        var first = _composer.Compose(conversation, Message("one"), "en", false);
        var second = _composer.Compose(conversation, Message("two"), "en", false);

        Assert.Equal($"/chat/{conversation.Id}", first.Url);
        Assert.StartsWith("/", first.Url);

        // Same conversation, different messages, SAME tag: the second notification replaces the
        // first on the device instead of stacking (FR-022).
        Assert.Equal($"chat:{conversation.Id}", first.Tag);
        Assert.Equal(first.Tag, second.Tag);

        // A different conversation is a different tag, so two people waiting still read as two
        // notifications (FR-023).
        Assert.NotEqual(first.Tag, _composer.Compose(Team(), Message("three"), "en", false).Tag);
    }

    // --- The recipient's language, never the sender's -------------------------

    [Theory]
    [InlineData("de", "hat dir eine Nachricht geschickt")]
    [InlineData("es", "te ha enviado un mensaje")]
    [InlineData("en", "sent you a message")]
    public void An_unreadable_message_still_goes_out_naming_the_sender(string culture, string expected)
    {
        // Feature 047: TryUnprotect returns false — it does not throw — for an envelope that cannot
        // be authenticated. The notification degrades to naming who wrote rather than being
        // dropped, mirroring the placeholder the app shows for one unreadable message (FR-021a).
        var unreadable = new ChatPushMessage(Guid.CreateVersion7(), "Anna Meyer", [1, 2, 3, 4, 5], false);

        var content = _composer.Compose(Direct(), unreadable, culture, false);

        Assert.Equal("Anna Meyer", content.Title);
        Assert.Equal(expected, content.Body);
    }

    [Fact]
    public void A_german_recipient_reads_german_whatever_the_sender_speaks()
    {
        // The pass groups recipients by their own stored language; there is no request culture to
        // fall back on, because there is no request.
        var content = _composer.Compose(Team(), new ChatPushMessage(Guid.CreateVersion7(), "Anna", [9, 9, 9], false), "de", false);

        Assert.Contains("im Chat geschrieben", content.Body);
    }

    [Fact]
    public void A_live_party_chat_is_named_in_the_readers_language()
    {
        // The one fallback that always fires: a live party conversation stores no name and nothing
        // derives one. The inbox says "Party chat" to everyone; a lock screen is composed per
        // recipient, so there is no reason to.
        var party = new ChatPushConversation(Guid.CreateVersion7(), ConversationKind.Party, null, null, null, null, null);

        Assert.Equal("Party-Chat", _composer.Compose(party, Message("hi"), "de", false).Title);
        Assert.Equal("Party chat", _composer.Compose(party, Message("hi"), "en", false).Title);
    }

    // --- The branches that carry no text --------------------------------------

    [Fact]
    public void A_message_that_is_only_attachments_says_so_rather_than_showing_nothing()
    {
        // A Member row with a live sender and a zero-length body is a real message made of files
        // (feature 049), not corruption — and asking the cipher about an empty array throws by
        // design, so the composer must branch on length FIRST.
        var content = _composer.Compose(Direct(), Message(string.Empty, attachments: true), "en", false);

        Assert.Equal("Anna Meyer", content.Title);
        Assert.Equal("sent an attachment", content.Body);
    }

    [Fact]
    public void An_attachment_in_a_group_still_names_the_sender()
    {
        var content = _composer.Compose(Team(), Message(string.Empty, attachments: true), "en", false);

        Assert.Equal("Hamburg Hammers", content.Title);
        Assert.Contains("Anna Meyer", content.Body);
    }

    [Fact]
    public void A_sender_whose_profile_is_gone_reads_as_the_neutral_stand_in()
    {
        var content = _composer.Compose(Direct(), Message("hello", sender: null), "en", false);

        Assert.False(string.IsNullOrWhiteSpace(content.Title));
        Assert.Equal(JuggerHub.Common.MemberPlaceholder.For("en"), content.Title);
    }

    // --- Truncation -----------------------------------------------------------

    [Fact]
    public void A_long_message_is_cut_short_with_a_visible_continuation()
    {
        // Messages run to 2000 characters and no lock screen shows that; sending one whole would
        // reproduce a long private message outside the platform for nobody's benefit (FR-020).
        var content = _composer.Compose(Direct(), Message(new string('a', 500)), "en", false);

        Assert.EndsWith("…", content.Body);
        Assert.True(content.Body.Length < 200, $"Preview was {content.Body.Length} characters.");
    }

    [Fact]
    public void A_short_message_is_not_touched()
    {
        var content = _composer.Compose(Direct(), Message("short"), "en", false);

        Assert.Equal("short", content.Body);
        Assert.DoesNotContain("…", content.Body);
    }

    [Fact]
    public void Truncation_does_not_split_an_emoji_in_half()
    {
        // Cutting on a UTF-16 unit rather than a text element would leave half a surrogate pair,
        // which renders as a replacement character on the device.
        var content = _composer.Compose(Direct(), Message(string.Concat(Enumerable.Repeat("🏃", 300))), "en", false);

        Assert.DoesNotContain('�', content.Body);
        Assert.EndsWith("…", content.Body);
    }

    // --- Per-viewer naming ----------------------------------------------------

    [Fact]
    public void An_inquiry_is_named_differently_for_its_two_sides()
    {
        // Feature 027: the requester sees what they are asking about; an admin sees who is asking
        // AND about what, because an admin of several teams needs to tell inquiries apart. This is
        // why the name is evaluated per recipient rather than once per conversation.
        var inquiry = new ChatPushConversation(
            Guid.CreateVersion7(), ConversationKind.TeamInquiry, null, "Hamburg Hammers", null, "Ada K.", Guid.CreateVersion7());

        var toRequester = _composer.Compose(inquiry, Message("any news?"), "en", isRequester: true);
        var toAdmin = _composer.Compose(inquiry, Message("any news?"), "en", isRequester: false);

        Assert.Equal("Hamburg Hammers", toRequester.Title);
        Assert.Equal("Ada K. · Hamburg Hammers", toAdmin.Title);
    }

    [Fact]
    public void An_archived_conversation_keeps_the_name_it_froze()
    {
        // Archival severs the link the name was derived from, so the stored name wins (019 R3a).
        var archived = new ChatPushConversation(
            Guid.CreateVersion7(), ConversationKind.Team, "Rheinfeuer", null, null, null, null);

        Assert.Equal("Rheinfeuer", _composer.Compose(archived, Message("hi"), "en", false).Title);
    }
}
