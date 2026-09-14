using JuggerHub.Common;
using JuggerHub.Services.Chat.Encryption;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Unit tests for chat attachment encryption at rest (feature 049 / #282). Pure — no API host and
/// no database, so these run without Docker, exactly like <see cref="ChatMessageCipherTests"/>.
/// </summary>
public sealed class ChatBlobCipherTests
{
    private const int HeaderSize = 29; // version(1) + nonce(12) + tag(16)

    private static byte[] Key(byte seed) => Enumerable.Repeat(seed, ChatEncryptionOptions.KeySizeBytes).ToArray();

    private static AesGcmChatBlobCipher Cipher(params ChatEncryptionKey[] keys) =>
        new(keys.Length == 0 ? [new ChatEncryptionKey(1, Key(0xA1))] : keys);

    private static byte[] Bytes(int length, byte seed = 0x7F) =>
        Enumerable.Range(0, length).Select(i => (byte)(seed + i)).ToArray();

    // --- Envelope shape --------------------------------------------------------

    [Fact]
    public void Envelope_is_header_plus_the_length_of_the_file()
    {
        var envelope = Cipher().Protect(Bytes(1024), Guid.CreateVersion7());

        Assert.Equal(HeaderSize + 1024, envelope.Length);
        Assert.Equal(1, envelope[0]); // version, deliberately outside the encryption
    }

    [Fact]
    public void A_file_round_trips_byte_for_byte()
    {
        var cipher = Cipher();
        var id = Guid.CreateVersion7();
        var original = Bytes(4096);

        var envelope = cipher.Protect(original, id);

        Assert.True(cipher.TryUnprotect(envelope, id, out var recovered));
        Assert.Equal(original, recovered);
    }

    [Fact]
    public void An_empty_file_round_trips_as_an_empty_file()
    {
        // The deliberate divergence from IChatMessageCipher, which throws on empty input so that a
        // deleted message's row visibly holds nothing. An empty FILE is a file a member can really
        // attach, and it must come back as one rather than being confused with an absent object.
        var cipher = Cipher();
        var id = Guid.CreateVersion7();

        var envelope = cipher.Protect([], id);

        Assert.Equal(HeaderSize, envelope.Length);
        Assert.True(cipher.TryUnprotect(envelope, id, out var recovered));
        Assert.Empty(recovered);
    }

    // --- The nonce is fresh every time -----------------------------------------

    [Fact]
    public void Same_bytes_and_same_row_produce_different_ciphertext()
    {
        var cipher = Cipher();
        var id = Guid.CreateVersion7();
        var content = Bytes(64);

        Assert.NotEqual(cipher.Protect(content, id), cipher.Protect(content, id));
    }

    // --- Associated data binds the ciphertext to ITS OWN row -------------------

    [Fact]
    public void A_ciphertext_moved_to_another_attachment_does_not_authenticate()
    {
        // The reason the id is associated data at all: an operator with database write access
        // cannot relocate a stored object between rows and have it read as that row's file. This
        // is the single assertion that would fail if someone "simplified" the binding away.
        var cipher = Cipher();
        var mine = Guid.CreateVersion7();
        var theirs = Guid.CreateVersion7();

        var envelope = cipher.Protect(Bytes(128), mine);

        Assert.False(cipher.TryUnprotect(envelope, theirs, out var recovered));
        Assert.Empty(recovered);
    }

    [Fact]
    public void A_tampered_ciphertext_does_not_authenticate()
    {
        var cipher = Cipher();
        var id = Guid.CreateVersion7();
        var envelope = cipher.Protect(Bytes(128), id);

        envelope[^1] ^= 0xFF;

        Assert.False(cipher.TryUnprotect(envelope, id, out _));
    }

    // --- Degrading, never throwing ---------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(HeaderSize - 1)]
    public void A_truncated_envelope_returns_false_rather_than_throwing(int length)
    {
        // A damaged object must degrade to ONE unavailable attachment, never to a failed
        // conversation (spec FR-028).
        Assert.False(Cipher().TryUnprotect(Bytes(length), Guid.CreateVersion7(), out var recovered));
        Assert.Empty(recovered);
    }

    [Fact]
    public void An_envelope_naming_an_unconfigured_key_version_returns_false()
    {
        var id = Guid.CreateVersion7();
        var envelope = Cipher(new ChatEncryptionKey(9, Key(0xC3))).Protect(Bytes(32), id);

        // A deployment that retired version 9 still reads every other row.
        var withoutNine = Cipher(new ChatEncryptionKey(1, Key(0xA1)));

        Assert.False(withoutNine.TryUnprotect(envelope, id, out _));
    }

    // --- Key set semantics: position carries the meaning -----------------------

    [Fact]
    public void The_first_configured_key_writes_and_the_rest_stay_readable()
    {
        var id = Guid.CreateVersion7();
        var content = Bytes(256);

        var old = Cipher(new ChatEncryptionKey(1, Key(0xA1)));
        var written = old.Protect(content, id);

        // Rotation is "prepend the new key": 2 writes from now on, 1 stays readable.
        var rotated = Cipher(new ChatEncryptionKey(2, Key(0xB2)), new ChatEncryptionKey(1, Key(0xA1)));

        Assert.Equal(2, rotated.Protect(content, id)[0]);
        Assert.True(rotated.TryUnprotect(written, id, out var recovered));
        Assert.Equal(content, recovered);
    }

    [Fact]
    public void Version_is_readable_without_decrypting()
    {
        var envelope = Cipher(new ChatEncryptionKey(7, Key(0xD4))).Protect(Bytes(8), Guid.CreateVersion7());

        Assert.Equal((byte)7, Cipher().VersionOf(envelope));
        Assert.Null(Cipher().VersionOf([]));
    }

    // --- The two ciphers do not interoperate, and that is the point ------------

    [Fact]
    public void A_message_envelope_does_not_decrypt_as_an_attachment_for_the_same_id()
    {
        // Same keys, same envelope format, same associated-data scheme — so the ONLY thing that
        // separates a message body from an attachment is which id it was bound to. This pins that
        // an id collision across the two tables still would not let one read as the other's
        // plaintext under a different row's id.
        var keys = new[] { new ChatEncryptionKey(1, Key(0xA1)) };
        var messageCipher = new AesGcmChatMessageCipher(keys);
        var blobCipher = new AesGcmChatBlobCipher(keys);

        var messageId = Guid.CreateVersion7();
        var attachmentId = Guid.CreateVersion7();

        var body = messageCipher.Protect("the pitch is booked", messageId);

        Assert.False(blobCipher.TryUnprotect(body, attachmentId, out _));
    }
}
