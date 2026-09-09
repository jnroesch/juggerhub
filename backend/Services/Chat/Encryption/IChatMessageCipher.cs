namespace JuggerHub.Services.Chat.Encryption;

/// <summary>
/// Turns chat message text into the bytes stored in <c>ChatMessages.BodyCipher</c>, and back
/// (feature 047 / #223). The single seam through which message text becomes storable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a service and not an EF value converter.</b> A converter was the obvious design
/// and #223 proposed it, but it cannot work here: a converter has no access to the request's
/// culture, so it cannot produce the localized "this message can't be displayed" placeholder;
/// it throws inside <c>ToListAsync()</c> materialisation, so one unreadable row would fail an
/// entire conversation; and it never sees the row's id, so the ciphertext could not be bound to
/// it. Decryption therefore happens in the chat services' DTO-mapping step, where both the culture
/// and the id are already in hand. See <c>specs/047-chat-message-encryption/research.md</c> §1.
/// </para>
/// <para>
/// <b>The message id is associated data, not decoration.</b> Binding the ciphertext to the row it
/// belongs to means an operator with database write access cannot move a ciphertext from one
/// message to another: the authentication tag fails and the message reads as unavailable rather
/// than as someone else's words.
/// </para>
/// </remarks>
public interface IChatMessageCipher
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> under the write key (the first configured version),
    /// bound to <paramref name="messageId"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="plaintext"/> is empty. "No text" is stored as a zero-length array by the
    /// caller and is deliberately not something this type can manufacture: encrypting the empty
    /// string would produce an envelope indistinguishable from a short message, and a deleted
    /// message's row would stop visibly holding nothing.
    /// </exception>
    byte[] Protect(string plaintext, Guid messageId);

    /// <summary>
    /// Decrypts a stored envelope. Returns <c>false</c> — never throws — when the envelope is
    /// truncated, names a key version that is not configured, or fails authentication. The caller
    /// renders a placeholder for that one message and leaves the rest of the conversation alone
    /// (FR-009).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="cipher"/> is empty. An empty array means "this row has no text", which is
    /// the caller's business to recognise before asking.
    /// </exception>
    bool TryUnprotect(byte[] cipher, Guid messageId, out string plaintext);

    /// <summary>
    /// The key version a stored envelope names, or <c>null</c> when it is too short to say. For
    /// operator-facing diagnostics only — it reads one plaintext byte and proves nothing about
    /// whether the row can actually be decrypted.
    /// </summary>
    byte? VersionOf(byte[] cipher);
}
