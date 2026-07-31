namespace JuggerHub.Services.Media;

/// <summary>Which kind of media an object holds — selects its key prefix (feature 035 / #97).</summary>
public enum MediaKind
{
    /// <summary>A member's profile picture.</summary>
    Avatar,

    /// <summary>A badge definition's catalogue icon.</summary>
    BadgeIcon,

    /// <summary>An achievement definition's catalogue icon.</summary>
    AchievementIcon,
}

/// <summary>
/// Generates storage keys for media objects (feature 035 / #97).
/// </summary>
/// <remarks>
/// <para>
/// Keys look like <c>avatars/9f2c4d1ab7e04c6f8a3d5e21bc09f4a7.webp</c>. The prefix exists for
/// operator legibility and lifecycle rules only — it is <b>not</b> a security boundary. The
/// container is private as a whole, and prefixes must never be used to grant differential public
/// access to one kind of media.
/// </para>
/// <para>
/// <b>Why <see cref="Guid.NewGuid"/> (UUIDv4) and not <c>Guid.CreateVersion7()</c>.</b> Constitution
/// Principle III mandates UUIDv7 for <em>primary keys</em>, and states the reason: a timestamp
/// prefix makes inserts append to the right edge of the B-tree instead of fragmenting it. None of
/// that applies here. A storage key is not a database key, is never range-scanned, and is stored in
/// exactly one column. What it must be is <b>unguessable</b> — and UUIDv7 is the wrong tool for
/// that precisely because its leading bits are a timestamp, which is partially predictable. Using
/// v4 here is an application of the principle's intent, not a departure from it: pick the
/// identifier whose properties match the job. Please do not "correct" this to v7.
/// </para>
/// <para>
/// Unguessability is defence in depth, not the primary control. The primary control is that the
/// container refuses public reads and that keys never leave the backend at all. This exists so that
/// a future misconfiguration of the former is not by itself enough to expose media.
/// </para>
/// </remarks>
public static class MediaObjectKey
{
    /// <summary>Longest key this scheme can produce, matching the database column width.</summary>
    public const int MaxLength = 200;

    /// <summary>
    /// Create a fresh, unguessable key for <paramref name="kind"/>. Called once per upload, before
    /// the first store attempt, so a retried write overwrites the same object.
    /// </summary>
    public static string Create(MediaKind kind) =>
        $"{Prefix(kind)}/{Guid.NewGuid():n}.webp";

    /// <summary>The key prefix (folder) for a media kind.</summary>
    public static string Prefix(MediaKind kind) => kind switch
    {
        MediaKind.Avatar => "avatars",
        MediaKind.BadgeIcon => "badge-icons",
        MediaKind.AchievementIcon => "achievement-icons",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown media kind."),
    };
}
