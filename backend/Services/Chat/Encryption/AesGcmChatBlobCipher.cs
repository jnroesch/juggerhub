using System.Security.Cryptography;
using JuggerHub.Common;

namespace JuggerHub.Services.Chat.Encryption;

/// <summary>
/// AES-256-GCM implementation of <see cref="IChatBlobCipher"/> (feature 049 / #282).
/// </summary>
/// <remarks>
/// <para>
/// <b>Envelope</b> — <c>[version:1][nonce:12][tag:16][ciphertext:n]</c>, byte-for-byte the same
/// shape <see cref="AesGcmChatMessageCipher"/> writes, for the same reasons: the version byte sits
/// outside the encryption so the key protecting an object is identifiable without trial
/// decryption, and the nonce is fresh per call because GCM's failure mode under nonce reuse is key
/// recovery rather than a leaked equality.
/// </para>
/// <para>
/// Associated data is the attachment's UUIDv7 id. <c>BaseEntity</c> assigns it in a field
/// initialiser, so it exists before the object is written and never changes afterwards — which is
/// what lets the key be minted, the object encrypted, and the row committed in that order.
/// </para>
/// <para>
/// Registered as a singleton: it holds the parsed key set and no per-request state.
/// </para>
/// </remarks>
public sealed class AesGcmChatBlobCipher : IChatBlobCipher
{
    private const int VersionSize = 1;
    private const int NonceSize = 12;   // AesGcm.NonceByteSizes.MaxSize — the value GCM is specified for
    private const int TagSize = 16;     // AesGcm.TagByteSizes.MaxSize — anything shorter weakens authentication

    /// <summary>Smallest envelope that can possibly be well-formed (an empty ciphertext).</summary>
    internal const int HeaderSize = VersionSize + NonceSize + TagSize;

    private readonly ChatEncryptionKey _writeKey;
    private readonly IReadOnlyDictionary<byte, byte[]> _keysByVersion;

    public AesGcmChatBlobCipher(IReadOnlyList<ChatEncryptionKey> keys)
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

    public byte[] Protect(byte[] plaintext, Guid attachmentId)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        // No empty-input guard, unlike the message cipher: an empty file is a file. See
        // IChatBlobCipher for why the two contracts differ here.
        var envelope = new byte[HeaderSize + plaintext.Length];

        envelope[0] = _writeKey.Version;
        var nonce = envelope.AsSpan(VersionSize, NonceSize);
        var tag = envelope.AsSpan(VersionSize + NonceSize, TagSize);
        var ciphertext = envelope.AsSpan(HeaderSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_writeKey.Key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(attachmentId));

        return envelope;
    }

    public bool TryUnprotect(byte[] cipher, Guid attachmentId, out byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(cipher);

        plaintext = [];

        // Truncated envelope: too short to even carry a header. Not an exception — a damaged object
        // must degrade to one unavailable attachment, never to a failed conversation.
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
            aes.Decrypt(nonce, ciphertext, tag, plainBytes, AssociatedData(attachmentId));
        }
        catch (CryptographicException)
        {
            // Authentication failed: tampered ciphertext, the wrong key for this version, or an
            // object moved from another row. All three are the same answer to the caller.
            return false;
        }

        plaintext = plainBytes;
        return true;
    }

    public byte? VersionOf(byte[] cipher) =>
        cipher is { Length: > 0 } ? cipher[0] : null;

    private static byte[] AssociatedData(Guid attachmentId) => attachmentId.ToByteArray();
}
