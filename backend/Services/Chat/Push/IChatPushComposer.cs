using JuggerHub.Entities;
using JuggerHub.Services.Notifications.Push;

namespace JuggerHub.Services.Chat.Push;

/// <summary>
/// Everything about a conversation that a notification needs, read once per conversation.
/// </summary>
/// <remarks>
/// The naming inputs are the same ones the inbox projects, because a notification and the inbox
/// must call a conversation the same thing (see <see cref="ChatDisplayName"/>). They are gathered
/// once and the name is then evaluated <em>per recipient</em>, because two of the six kinds name
/// themselves differently for different viewers: a direct conversation is "the other person", and
/// an inquiry is the team or event to its requester but "{requester} · {team}" to an admin.
/// </remarks>
public sealed record ChatPushConversation(
    Guid Id,
    ConversationKind Kind,
    string? StoredName,
    string? TeamName,
    string? EventName,
    string? RequesterName,
    Guid? RequesterUserId);

/// <summary>
/// The one message a notification is about, and who wrote it.
/// </summary>
/// <param name="Id">Needed to decrypt: the ciphertext is bound to it as associated data (feature 047).</param>
/// <param name="SenderName">Already resolved, or null when the sender's profile is gone or hidden.</param>
/// <param name="BodyCipher">The stored envelope. <b>Zero-length means the row holds no text</b>, which is a real message made of attachments — never corruption.</param>
/// <param name="HasAttachments">Whether files came with it (feature 049).</param>
public sealed record ChatPushMessage(
    Guid Id,
    string? SenderName,
    byte[] BodyCipher,
    bool HasAttachments);

/// <summary>
/// Turns one chat message into the four fields that reach a device (feature 056).
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not an extension of <c>PushContentComposer</c>.</b> That one is typed to a
/// <see cref="NotificationType"/> and to the in-app row's stored payload JSON, and chat has
/// neither — feature 019's FR-051 is the whole reason. Making chat fit it would mean inventing a
/// fake notification type, which is precisely the reversal this design exists to avoid. Both
/// produce the same <see cref="PushContent"/> and hand it to the same dispatcher; that record is
/// the seam, exactly as feature 055 intended.
/// </para>
/// <para>
/// Pure: no database, no network, no clock. Everything it needs arrives as an argument, so every
/// branch of it is unit-testable.
/// </para>
/// </remarks>
public interface IChatPushComposer
{
    /// <summary>
    /// Composes the notification one recipient will see.
    /// </summary>
    /// <param name="conversation">Read once per conversation, evaluated per recipient.</param>
    /// <param name="message">The newest message this recipient has not read.</param>
    /// <param name="culture">
    /// The <b>recipient's</b> language — never the sender's, and never the request's. There is no
    /// request: this runs on a background pass.
    /// </param>
    /// <param name="isRequester">Whether this recipient is the requester of an inquiry thread.</param>
    PushContent Compose(
        ChatPushConversation conversation,
        ChatPushMessage message,
        string culture,
        bool isRequester);
}
