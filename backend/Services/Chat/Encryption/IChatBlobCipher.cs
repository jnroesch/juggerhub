namespace JuggerHub.Services.Chat.Encryption;

/// <summary>
/// Turns the bytes of a chat attachment into the bytes stored in the media store, and back
/// (feature 049 / #282). The single seam through which an attached file becomes storable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a second interface rather than a widened <see cref="IChatMessageCipher"/>.</b>
/// That one's contract is <em>text</em>, and it carries a rule that only makes sense for text:
/// <c>Protect</c> refuses an empty string, because an envelope around <c>""</c> is 29 bytes
/// indistinguishable from a short message and would destroy the observability of "this row holds
/// nothing" (feature 047, data-model D2). An empty <em>file</em> is a different question with a
/// different answer, and a caller reasoning about attachments should not have to learn why a text
/// API refuses empties in order to get it right. Two seams, one contract each.
/// </para>
/// <para>
/// <b>It is the same key material, though.</b> Both ciphers read <c>ChatEncryptionOptions</c> and
/// share the envelope format, the version-byte convention and the fail-fast startup guard. One
/// secret to deliver, one secret to rotate. A separate attachment key would be a second thing to
/// keep in sync for no gain — anything that can read one can read the other.
/// </para>
/// <para>
/// <b>The associated data is the attachment's own id, not its message's.</b> Binding to the
/// message would let an operator with database write access swap two attachments <em>within</em> a
/// message and have both still authenticate. Binding to the attachment means a ciphertext moved
/// anywhere — to another attachment, or into a message body — fails its tag and reads as
/// unavailable rather than as someone else's file.
/// </para>
/// </remarks>
public interface IChatBlobCipher
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> under the write key (the first configured version),
    /// bound to <paramref name="attachmentId"/>.
    /// </summary>
    /// <remarks>
    /// A zero-length <paramref name="plaintext"/> is accepted and produces a well-formed envelope.
    /// Unlike message text, "an empty file" is a thing a member can genuinely attach, and it must
    /// round-trip as one rather than being confused with "this row holds nothing" — which for an
    /// attachment is not a state that exists, since a row without an object would have no reason
    /// to be there.
    /// </remarks>
    byte[] Protect(byte[] plaintext, Guid attachmentId);

    /// <summary>
    /// Decrypts a stored envelope. Returns <c>false</c> — never throws — when the envelope is
    /// truncated, names a key version that is not configured, or fails authentication. The caller
    /// renders an unavailable placeholder for that one attachment and leaves the rest of the
    /// conversation alone (spec FR-028).
    /// </summary>
    bool TryUnprotect(byte[] cipher, Guid attachmentId, out byte[] plaintext);

    /// <summary>
    /// The key version a stored envelope names, or <c>null</c> when it is too short to say. For
    /// operator-facing diagnostics only — it reads one plaintext byte and proves nothing about
    /// whether the object can actually be decrypted.
    /// </summary>
    byte? VersionOf(byte[] cipher);
}
