using JuggerHub.Common;
using JuggerHub.Entities;
using JuggerHub.Services.Chat.Encryption;
using JuggerHub.Services.Notifications.Push;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Chat.Push;

/// <inheritdoc cref="IChatPushComposer" />
public sealed class ChatPushComposer : IChatPushComposer
{
    /// <summary>
    /// Appended to a message that was cut short. A single character, so it costs almost nothing of
    /// the budget it signals the end of.
    /// </summary>
    private const string Ellipsis = "…";

    private readonly IChatMessageCipher _cipher;
    private readonly IPushLocalizer _localizer;
    private readonly ChatPushOptions _options;

    public ChatPushComposer(
        IChatMessageCipher cipher,
        IPushLocalizer localizer,
        IOptions<ChatPushOptions> options)
    {
        _cipher = cipher;
        _localizer = localizer;
        _options = options.Value;
    }

    /// <inheritdoc />
    public PushContent Compose(
        ChatPushConversation conversation,
        ChatPushMessage message,
        string culture,
        bool isRequester)
    {
        var placeholder = MemberPlaceholder.For(culture);
        var sender = string.IsNullOrWhiteSpace(message.SenderName) ? placeholder : message.SenderName;
        var isDirect = conversation.Kind == ConversationKind.Direct;

        // A direct conversation IS the other person, so naming the sender names the conversation
        // (FR-018). Every other kind needs its own name, or a member of two team chats cannot tell
        // which one is talking.
        var title = isDirect
            ? sender
            : ChatDisplayName.For(
                conversation.Kind,
                conversation.StoredName,
                conversation.TeamName,
                // For a direct conversation the recipient's "other" is always the sender: there
                // are exactly two members and the sender is not the recipient. Passed anyway so
                // the helper behaves identically wherever it is called from.
                sender,
                conversation.EventName,
                conversation.RequesterName,
                isRequester,
                placeholder,
                Fallbacks(culture));

        return new PushContent(title, Body(message, sender, isDirect, culture), Url(conversation.Id), Tag(conversation.Id));
    }

    /// <summary>
    /// What the notification says happened.
    /// </summary>
    /// <remarks>
    /// <b>Order matters and the first branch is the trap.</b> A zero-length <c>BodyCipher</c> on a
    /// member message is a real message made of attachments (feature 049), and
    /// <see cref="IChatMessageCipher.TryUnprotect"/> throws <see cref="ArgumentException"/> on an
    /// empty array by design — it returns <c>false</c> only for an envelope that is corrupt. Ask it
    /// about an empty body and every photo-only message becomes a logged exception.
    /// </remarks>
    private string Body(ChatPushMessage message, string sender, bool isDirect, string culture)
    {
        if (message.BodyCipher.Length == 0)
        {
            // No text at all. Say something was sent rather than showing an empty preview
            // (FR-021b). A message with neither text nor attachments cannot be sent, so this is
            // the attachment case; the wording holds either way.
            return isDirect
                ? _localizer.Get("chat.sentAttachment", culture)
                : _localizer.Get("chat.sentAttachmentIn", culture, sender);
        }

        if (!_cipher.TryUnprotect(message.BodyCipher, message.Id, out var text))
        {
            // The text cannot be read back — a retired key, a truncated envelope, a failed
            // authentication tag. The notification still goes out naming who wrote, with no
            // preview (FR-021a): the same posture the app already takes when it shows a
            // placeholder for one message rather than failing the whole conversation (047).
            //
            // Nothing about the failure is logged here. The pass that called this logs counts and
            // ids only, and ChatMessageService already reports an unreadable row when a member
            // actually opens the conversation, with the key version an operator needs.
            return isDirect
                ? _localizer.Get("chat.sentMessage", culture)
                : _localizer.Get("chat.sentMessageIn", culture, sender);
        }

        var preview = Truncate(text);

        // A direct message needs no attribution — the title is the sender. Everywhere else the
        // sender goes in front, because the title is the conversation.
        return isDirect ? preview : _localizer.Get("chat.groupBody", culture, sender, preview);
    }

    /// <summary>
    /// Cuts a message down to what a lock screen will show anyway.
    /// </summary>
    /// <remarks>
    /// Messages run to <c>ChatConstants.MaxMessageLength</c> (2000 characters) and no notification
    /// surface displays anything close, so sending one whole would reproduce a long private message
    /// outside the platform for nobody's benefit. Cut on a text element rather than a UTF-16 unit,
    /// so an emoji or a combining accent at the boundary is not split into something unrenderable.
    /// </remarks>
    private string Truncate(string text)
    {
        var limit = _options.PreviewLength;
        if (limit <= 0 || text.Length <= limit)
        {
            return text;
        }

        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        var end = 0;
        var taken = 0;
        while (enumerator.MoveNext() && taken < limit)
        {
            end = enumerator.ElementIndex + enumerator.GetTextElement().Length;
            taken++;
        }

        return end >= text.Length ? text : string.Concat(text.AsSpan(0, end).TrimEnd(), Ellipsis);
    }

    /// <summary>
    /// Where the notification opens: the conversation itself. App-relative and built from a
    /// <see cref="Guid"/>, never from anything a member typed, so the service worker's refusal to
    /// follow an absolute URL can never fire on it.
    /// </summary>
    private static string Url(Guid conversationId) => $"/chat/{conversationId}";

    /// <summary>
    /// The collapse key — <b>per conversation, not per message</b>.
    /// </summary>
    /// <remarks>
    /// This is what makes four messages arriving while a member is away leave <em>one</em>
    /// notification on the device rather than four (FR-022), and it works across passes as well as
    /// within one: a later arrival replaces the earlier notification instead of stacking. The
    /// dispatcher also sends it as the push message's topic, so a push service replaces a message
    /// it is still holding undelivered. Two different conversations get two different tags, so a
    /// member can still see that two people are waiting on them (FR-023).
    /// </remarks>
    private static string Tag(Guid conversationId) => $"chat:{conversationId}";

    /// <summary>
    /// The generic conversation names, in the recipient's language.
    /// </summary>
    /// <remarks>
    /// The inbox ships these in English to everyone, which is tolerable inside an app whose chrome
    /// the reader can see. A lock screen has no chrome and is composed per recipient anyway, so
    /// there is no reason to send a German member the words "Party chat" — which is not a rare
    /// path: it is what every live party conversation is called.
    /// </remarks>
    private ChatNameFallbacks Fallbacks(string culture) => new(
        _localizer.Get("chat.name.group", culture),
        _localizer.Get("chat.name.team", culture),
        _localizer.Get("chat.name.party", culture),
        _localizer.Get("chat.name.other", culture));
}
