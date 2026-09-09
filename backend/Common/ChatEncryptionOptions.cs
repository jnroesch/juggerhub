namespace JuggerHub.Common;

/// <summary>One configured encryption key: the version stamped into every row it protects, and the
/// 32 raw bytes. Never logged, never serialized, never leaves the process.</summary>
public sealed record ChatEncryptionKey(byte Version, byte[] Key);

/// <summary>
/// Configuration for chat message encryption at rest (feature 047 / #223). Bound from the
/// <c>Chat:Encryption</c> section, which in practice is a single secret value delivered like every
/// other one — <c>.env</c> locally, GitHub Environments → Kubernetes Secret when deployed. No Key
/// Vault (Principle V).
/// </summary>
/// <remarks>
/// <para>
/// <b>One value, and the first entry writes.</b> The obvious shape is a key list plus a separate
/// "which one is active" setting — two secrets that can disagree, silently, until someone reads a
/// message. Making position carry the meaning removes that failure mode: there is nothing to keep
/// in sync, and rotating is "prepend the new key", a single edit to a single secret.
/// </para>
/// <para>
/// <b>There is no setting that disables encryption.</b> A missing or malformed value throws at
/// startup (<see cref="Parse"/>) rather than degrading to plaintext — storing message text in the
/// clear is the state this feature exists to leave behind, so it must not be reachable by
/// misconfiguration.
/// </para>
/// <para>
/// <b>No message produced here may contain key material.</b> Every diagnostic identifies an entry
/// by its 1-based position, never by its content; an exception that helpfully echoed the bad value
/// would put a key into logs, and configuration errors are exactly when logs get shared.
/// </para>
/// </remarks>
public sealed class ChatEncryptionOptions
{
    public const string SectionName = "Chat:Encryption";

    /// <summary>Required key size for AES-256.</summary>
    public const int KeySizeBytes = 32;

    /// <summary>
    /// <c>version:base64key</c> entries separated by <c>;</c>, e.g. <c>2:AAA…;1:BBB…</c>.
    /// <b>The first entry is the write key.</b> The rest exist only so rows stamped with an older
    /// version stay readable (FR-006).
    /// </summary>
    public string Keys { get; set; } = string.Empty;

    /// <summary>
    /// Parses and validates <see cref="Keys"/>, preserving order so the first entry is the writer.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The value is missing, an entry is malformed, a key is not exactly
    /// <see cref="KeySizeBytes"/> bytes, a version is outside 1–255, or a version repeats. The
    /// message names the configuration key and the offending position, never its content.
    /// </exception>
    public IReadOnlyList<ChatEncryptionKey> Parse()
    {
        if (string.IsNullOrWhiteSpace(Keys))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Keys is required: chat messages are encrypted at rest and the " +
                "application will not start without a key. Expected one or more 'version:base64key' " +
                "entries separated by ';', the first of which is used for new messages.");
        }

        var entries = Keys.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new List<ChatEncryptionKey>(entries.Length);
        var seen = new HashSet<byte>();

        for (var i = 0; i < entries.Length; i++)
        {
            var position = i + 1;
            var separator = entries[i].IndexOf(':');
            if (separator <= 0 || separator == entries[i].Length - 1)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Keys entry {position} is malformed. Expected 'version:base64key'.");
            }

            if (!byte.TryParse(entries[i][..separator], out var version) || version == 0)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Keys entry {position} has an invalid version. Expected an integer 1-255.");
            }

            if (!seen.Add(version))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Keys declares version {version} more than once. Each version identifies " +
                    "exactly one key; a duplicate makes it ambiguous which key wrote a row.");
            }

            byte[] key;
            try
            {
                key = Convert.FromBase64String(entries[i][(separator + 1)..]);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Keys entry {position} is not valid base64.");
            }

            if (key.Length != KeySizeBytes)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Keys entry {position} decodes to {key.Length} bytes; AES-256 requires " +
                    $"exactly {KeySizeBytes}.");
            }

            parsed.Add(new ChatEncryptionKey(version, key));
        }

        return parsed;
    }
}
