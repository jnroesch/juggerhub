using System.Security.Cryptography;
using System.Text;
using JuggerHub.Common;
using JuggerHub.Services.Chat.Encryption;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Unit tests for chat message encryption at rest (feature 047 / #223). Pure — no API host and no
/// database, so these run without Docker. Covers contracts/chat-message-cipher.md cases C1–C11.
/// </summary>
public sealed class ChatMessageCipherTests
{
    private const int HeaderSize = 29; // version(1) + nonce(12) + tag(16)

    private static byte[] Key(byte seed) => Enumerable.Repeat(seed, ChatEncryptionOptions.KeySizeBytes).ToArray();

    private static AesGcmChatMessageCipher Cipher(params ChatEncryptionKey[] keys) =>
        new(keys.Length == 0 ? [new ChatEncryptionKey(1, Key(0xA1))] : keys);

    // --- C1/C11: envelope shape ------------------------------------------------

    [Fact]
    public void Envelope_is_header_plus_the_utf8_length_of_the_plaintext()
    {
        var cipher = Cipher();

        var envelope = cipher.Protect("hello", Guid.CreateVersion7());

        Assert.Equal(HeaderSize + 5, envelope.Length);
        Assert.Equal(1, envelope[0]); // version, deliberately outside the encryption (FR-005)
    }

    [Fact]
    public void Longest_allowed_message_of_four_byte_characters_fits()
    {
        // 2 000 characters is the player-facing limit; an emoji is two UTF-16 chars and four
        // UTF-8 bytes, so this is the widest a legal message can be on disk (FR-011).
        var text = string.Concat(Enumerable.Repeat("\U0001F600", 1000));
        Assert.Equal(2000, text.Length);

        var envelope = Cipher().Protect(text, Guid.CreateVersion7());

        Assert.Equal(HeaderSize + 4000, envelope.Length);
    }

    // --- C2: the nonce is fresh every time -------------------------------------

    [Fact]
    public void Same_text_and_same_row_produce_different_ciphertext()
    {
        var cipher = Cipher();
        var id = Guid.CreateVersion7();

        var first = cipher.Protect("see you at training", id);
        var second = cipher.Protect("see you at training", id);

        // Nonce reuse under GCM is catastrophic, not merely a leaked equality — this is the
        // assertion that a future "cache the AesGcm instance and the nonce" optimisation trips.
        Assert.NotEqual(first, second);
        Assert.NotEqual(first.AsSpan(1, 12).ToArray(), second.AsSpan(1, 12).ToArray());
    }

    // --- C3: round-trip --------------------------------------------------------

    [Theory]
    [InlineData("plain ascii")]
    [InlineData("emoji \U0001F3C6 and \U0001F94A")]
    [InlineData("combining marks: é ä ñ")]
    [InlineData("line one\nline two\r\nline three")]
    [InlineData("umlauts and ß, acentos, ¿signos?")]
    [InlineData("   leading and trailing kept verbatim   ")]
    public void Round_trip_preserves_the_text_exactly(string text)
    {
        var cipher = Cipher();
        var id = Guid.CreateVersion7();

        Assert.True(cipher.TryUnprotect(cipher.Protect(text, id), id, out var back));
        Assert.Equal(text, back);
    }

    [Fact]
    public void Round_trip_preserves_a_two_thousand_character_message()
    {
        var text = new string('x', 2000);
        var cipher = Cipher();
        var id = Guid.CreateVersion7();

        Assert.True(cipher.TryUnprotect(cipher.Protect(text, id), id, out var back));
        Assert.Equal(text, back);
    }

    // --- C4/C5: empty is the caller's business ---------------------------------

    [Fact]
    public void Protecting_empty_text_throws_rather_than_manufacturing_an_envelope()
    {
        // A 29-byte envelope around "" is indistinguishable from a short message, which would
        // destroy the one thing a deleted row is supposed to show: that it holds nothing.
        Assert.Throws<ArgumentException>(() => Cipher().Protect(string.Empty, Guid.CreateVersion7()));
    }

    [Fact]
    public void Unprotecting_an_empty_array_throws_rather_than_reporting_a_failure()
    {
        // Empty means "this row has no text" — a fact the caller already knows. Returning false
        // here would blur it into "this row is corrupt".
        Assert.Throws<ArgumentException>(() => Cipher().TryUnprotect([], Guid.CreateVersion7(), out _));
    }

    // --- C6: bound to the row --------------------------------------------------

    [Fact]
    public void A_ciphertext_moved_to_another_row_does_not_decrypt()
    {
        var cipher = Cipher();
        var envelope = cipher.Protect("meet at the north pitch", Guid.CreateVersion7());

        Assert.False(cipher.TryUnprotect(envelope, Guid.CreateVersion7(), out var text));
        Assert.Equal(string.Empty, text);
    }

    // --- C7: authentication, not a checksum ------------------------------------

    [Theory]
    [InlineData(0)]   // version byte
    [InlineData(5)]   // nonce
    [InlineData(20)]  // tag
    [InlineData(31)]  // ciphertext
    public void A_single_flipped_byte_anywhere_is_rejected(int index)
    {
        var cipher = Cipher();
        var id = Guid.CreateVersion7();
        var envelope = cipher.Protect("a message long enough to have a body", id);

        envelope[index] ^= 0xFF;

        Assert.False(cipher.TryUnprotect(envelope, id, out _));
    }

    // --- C8: truncation --------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(HeaderSize - 1)]
    public void A_truncated_envelope_returns_false_without_throwing(int length)
    {
        var cipher = Cipher();

        Assert.False(cipher.TryUnprotect(new byte[length], Guid.CreateVersion7(), out var text));
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void An_envelope_of_exactly_the_header_size_is_well_formed_but_fails_authentication()
    {
        // Not an exception and not a crash: this is the shape a corrupted row most plausibly takes.
        Assert.False(Cipher().TryUnprotect(new byte[HeaderSize], Guid.CreateVersion7(), out _));
    }

    // --- C9: unknown version ---------------------------------------------------

    [Fact]
    public void A_version_that_is_not_configured_returns_false()
    {
        var id = Guid.CreateVersion7();
        var envelope = Cipher(new ChatEncryptionKey(9, Key(0x09))).Protect("older", id);

        var current = Cipher(new ChatEncryptionKey(1, Key(0xA1)));

        Assert.False(current.TryUnprotect(envelope, id, out _));
        Assert.Equal((byte)9, current.VersionOf(envelope)); // still identifiable, which is the point of FR-005
    }

    [Fact]
    public void The_right_version_with_the_wrong_key_bytes_returns_false()
    {
        var id = Guid.CreateVersion7();
        var envelope = Cipher(new ChatEncryptionKey(1, Key(0x11))).Protect("older", id);

        Assert.False(Cipher(new ChatEncryptionKey(1, Key(0x22))).TryUnprotect(envelope, id, out _));
    }

    // --- C10: several versions, the first one writes ---------------------------

    [Fact]
    public void The_first_configured_key_writes_and_every_configured_key_reads()
    {
        var id = Guid.CreateVersion7();
        var old = Cipher(new ChatEncryptionKey(1, Key(0x11))).Protect("written under v1", id);

        var rotated = Cipher(
            new ChatEncryptionKey(2, Key(0x22)),
            new ChatEncryptionKey(1, Key(0x11)));

        var fresh = rotated.Protect("written under v2", id);

        Assert.Equal(2, fresh[0]);
        Assert.True(rotated.TryUnprotect(fresh, id, out var newText));
        Assert.Equal("written under v2", newText);
        Assert.True(rotated.TryUnprotect(old, id, out var oldText));
        Assert.Equal("written under v1", oldText);
    }

    [Fact]
    public void Retiring_a_key_makes_only_its_rows_unreadable()
    {
        // The rotation-gone-wrong case, and also the FR-009 path: the row is not lost to an
        // exception, it is reported as unreadable so one message can carry a placeholder.
        var id = Guid.CreateVersion7();
        var old = Cipher(new ChatEncryptionKey(1, Key(0x11))).Protect("written under v1", id);
        var afterRetirement = Cipher(new ChatEncryptionKey(2, Key(0x22)));

        var fresh = afterRetirement.Protect("still fine", id);

        Assert.False(afterRetirement.TryUnprotect(old, id, out _));
        Assert.True(afterRetirement.TryUnprotect(fresh, id, out _));
    }

    // --- Nothing observable leaks ---------------------------------------------

    [Fact]
    public void The_stored_bytes_contain_no_fragment_of_the_plaintext()
    {
        const string text = "meet at the north pitch at 18:00";
        var envelope = Cipher().Protect(text, Guid.CreateVersion7());

        var asLatin1 = Encoding.Latin1.GetString(envelope);
        Assert.DoesNotContain("pitch", asLatin1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("18:00", asLatin1, StringComparison.Ordinal);
        Assert.DoesNotContain(text, asLatin1, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_different_messages_of_the_same_length_are_not_distinguishable_by_content()
    {
        var cipher = Cipher();
        var id = Guid.CreateVersion7();

        var a = cipher.Protect("aaaaaaaaaa", id);
        var b = cipher.Protect("bbbbbbbbbb", id);

        Assert.Equal(a.Length, b.Length);
        Assert.NotEqual(a.AsSpan(HeaderSize).ToArray(), b.AsSpan(HeaderSize).ToArray());
    }

    [Fact]
    public void A_random_envelope_is_rejected_rather_than_decoded_into_mojibake()
    {
        var envelope = new byte[HeaderSize + 16];
        RandomNumberGenerator.Fill(envelope);
        envelope[0] = 1; // a configured version, so it gets past the lookup

        Assert.False(Cipher().TryUnprotect(envelope, Guid.CreateVersion7(), out _));
    }
}
