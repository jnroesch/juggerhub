using System.Security.Cryptography;
using System.Text;
using JuggerHub.Common;

namespace JuggerHub.Services.Chat.Encryption;

/// <summary>
/// AES-256-GCM implementation of <see cref="IChatMessageCipher"/> (feature 047 / #223).
/// </summary>
/// <remarks>
/// <para>
/// <b>Envelope</b> — <c>[version:1][nonce:12][tag:16][ciphertext:n]</c>.
/// </para>
/// <para>
/// The version byte is deliberately <em>outside</em> the encryption and fixed-width: FR-005
/// requires that the key protecting a row be identifiable without trial decryption, by the code and
/// by an operator with <c>psql</c> alike.
/// </para>
/// <para>
/// The nonce is generated fresh for every call. GCM's failure mode under nonce reuse is key
/// recovery, not merely a leaked equality — so it is never derived from the row, never a counter,
/// and never cached.
/// </para>
/// <para>
/// Associated data is the message's UUIDv7 id. <c>BaseEntity</c> assigns it in a field
/// initialiser, so it exists before the row is inserted and never changes afterwards.
/// </para>
/// <para>
/// Registered as a singleton: it holds the parsed key set and no per-request state.
/// <see cref="AesGcm"/> instances are constructed per call rather than cached — they are cheap,
/// and a shared instance would have to be synchronised.
/// </para>
/// </remarks>
public sealed class AesGcmChatMessageCipher : IChatMessageCipher
{
    private const int VersionSize = 1;
    private const int NonceSize = 12;   // AesGcm.NonceByteSizes.MaxSize — the value GCM is specified for
    private const int TagSize = 16;     // AesGcm.TagByteSizes.MaxSize — anything shorter weakens authentication

    /// <summary>Smallest envelope that can possibly be well-formed (an empty ciphertext).</summary>
    internal const int HeaderSize = VersionSize + NonceSize + TagSize;

    private readonly ChatEncryptionKey _writeKey;
    private readonly IReadOnlyDictionary<byte, byte[]> _keysByVersion;

    public AesGcmChatMessageCipher(IReadOnlyList<ChatEncryptionKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            throw new ArgumentException("At least one key is required.", nameof(keys));
        }

        // Position carries the meaning — see ChatEncryptionOptions. The first entry writes.
        _writeKey = keys[0];
        _keysByVersion = keys.ToDictionary(k => k.Version, k => k.Key);
    }

    public byte[] Protect(string plaintext, Guid messageId)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length == 0)
        {
            throw new ArgumentException(
                "Empty message text is stored as a zero-length array, not as an envelope — see IChatMessageCipher.",
                nameof(plaintext));
        }

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var envelope = new byte[HeaderSize + plainBytes.Length];

        envelope[0] = _writeKey.Version;
        var nonce = envelope.AsSpan(VersionSize, NonceSize);
        var tag = envelope.AsSpan(VersionSize + NonceSize, TagSize);
        var ciphertext = envelope.AsSpan(HeaderSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_writeKey.Key, TagSize);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag, AssociatedData(messageId));

        return envelope;
    }

    public bool TryUnprotect(byte[] cipher, Guid messageId, out string plaintext)
    {
        ArgumentNullException.ThrowIfNull(cipher);
        if (cipher.Length == 0)
        {
            throw new ArgumentException(
                "An empty array means the row holds no text; recognise that before calling.",
                nameof(cipher));
        }

        plaintext = string.Empty;

        // Truncated envelope: too short to even carry a header. Not an exception — a corrupted row
        // must degrade to one unavailable message, never to a failed page.
        if (cipher.Length < HeaderSize)
        {
            return false;
        }

        if (!_keysByVersion.TryGetValue(cipher[0], out var key))
        {
            return false;
        }

        var nonce = cipher.AsSpan(VersionSize, NonceSize);
        var tag = cipher.AsSpan(VersionSize + NonceSize, TagSize);
        var ciphertext = cipher.AsSpan(HeaderSize);
        var plainBytes = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plainBytes, AssociatedData(messageId));
        }
        catch (CryptographicException)
        {
            // Authentication failed: tampered ciphertext, the wrong key for this version, or a
            // ciphertext moved from another row. All three are the same answer to the caller.
            return false;
        }

        plaintext = Encoding.UTF8.GetString(plainBytes);
        return true;
    }

    public byte? VersionOf(byte[] cipher) =>
        cipher is { Length: > 0 } ? cipher[0] : null;

    private static byte[] AssociatedData(Guid messageId) => messageId.ToByteArray();
}
