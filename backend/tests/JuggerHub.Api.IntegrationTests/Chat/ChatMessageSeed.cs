using JuggerHub.Common;
using JuggerHub.Entities;
using JuggerHub.Services.Chat.Encryption;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Builds <see cref="ChatMessage"/> rows for tests that seed a conversation directly through the
/// DbContext rather than through the API (feature 047).
/// </summary>
/// <remarks>
/// Message text is encrypted at rest, so a test can no longer assign a string to the row. It could
/// resolve <see cref="IChatMessageCipher"/> from the host, but a plain helper keeps seeding a
/// one-liner and works in tests that have a DbContext but no scope to hand. The key is the test
/// host's own (<see cref="JuggerHubApiFactory.TestEncryptionKey"/>), so seeded rows read back
/// through the API exactly like rows the API wrote.
/// </remarks>
internal static class ChatMessageSeed
{
    private static readonly IChatMessageCipher Cipher =
        new AesGcmChatMessageCipher(new ChatEncryptionOptions { Keys = JuggerHubApiFactory.TestEncryptionKey }.Parse());

    /// <summary>A member's message in an existing conversation.</summary>
    internal static ChatMessage Member(Guid conversationId, Guid senderId, string body)
    {
        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Kind = ChatMessageKind.Member,
        };
        // After construction: the ciphertext is bound to the row's id, which BaseEntity assigns in
        // its field initialiser.
        message.BodyCipher = Cipher.Protect(body, message.Id);
        return message;
    }

    /// <summary>A member's message attached to a conversation that is itself being inserted.</summary>
    internal static ChatMessage Member(Conversation conversation, Guid senderId, string body)
    {
        var message = new ChatMessage
        {
            Conversation = conversation,
            SenderId = senderId,
            Kind = ChatMessageKind.Member,
        };
        message.BodyCipher = Cipher.Protect(body, message.Id);
        return message;
    }

    /// <summary>Encrypts as the test host would, for asserting on a raw row.</summary>
    internal static byte[] Protect(string body, Guid messageId) => Cipher.Protect(body, messageId);

    /// <summary>Decrypts as the test host would, for asserting on a raw row.</summary>
    internal static bool TryUnprotect(byte[] cipher, Guid messageId, out string body) =>
        Cipher.TryUnprotect(cipher, messageId, out body);
}
