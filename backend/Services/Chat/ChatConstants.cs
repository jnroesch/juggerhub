namespace JuggerHub.Services.Chat;

/// <summary>
/// Scale constants for chat (feature 019). Fixed here rather than configured — these are product
/// decisions from the spec/research, not per-environment knobs, and the constitution requires local,
/// Dev and Prod to differ only in configuration and secrets, never in behaviour.
/// </summary>
public static class ChatConstants
{
    /// <summary>
    /// Comfortably above a long chat message, far below a payload that would bloat a row or the inbox
    /// preview. Enforced server-side — the client's own check is UX only.
    /// </summary>
    public const int MaxMessageLength = 2000;

    /// <summary>
    /// Above the largest plausible manual group (a big team plus friends), and bounds the per-message
    /// realtime fan-out cost, since delivery is one push per participant. Team and party chats are not
    /// subject to it — their size is the roster's business.
    /// </summary>
    public const int MaxGroupMembers = 50;

    /// <summary>Matches the shared PaginationRequest default so the inbox contract is uniform with every other list.</summary>
    public const int InboxPageSize = 20;

    /// <summary>One screen-plus of history; keyset-paged backwards on the message's UUIDv7 id.</summary>
    public const int MessagePageSize = 30;

    /// <summary>Upper bound for a single history page, mirroring PaginationRequest's hard max.</summary>
    public const int MaxMessagePageSize = 100;

    /// <summary>
    /// How long a typing signal stays live. Deliberately longer than the client's ~3s debounce, so a
    /// steadily-typing player's indicator never flickers between signals — and short enough that a
    /// player who closes their tab mid-word stops "typing" almost immediately.
    /// </summary>
    public const int TypingExpirySeconds = 5;

    /// <summary>Minimum search term length, so a one-character query cannot scan every message a player can see.</summary>
    public const int MinSearchTermLength = 2;

    /// <summary>
    /// How many files may ride on one message (feature 049). Enough for a set of photos from a
    /// training session; low enough that the per-message fan-out, the thread layout and the
    /// transport cap all stay predictable.
    /// </summary>
    public const int MaxAttachmentsPerMessage = 10;

    /// <summary>
    /// Largest single upload, in bytes (feature 049). Comfortably covers a phone photo and a
    /// tournament PDF without inviting video, and matches the generous-input posture of the
    /// avatar endpoint.
    /// </summary>
    /// <remarks>
    /// Enforced against the <em>input</em>, before any processing — an image's stored size is much
    /// smaller after normalization, so checking the output would let a 40 MB upload through on the
    /// strength of what it shrank to, having already been decoded.
    /// </remarks>
    public const int MaxAttachmentBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Longest file name retained for display, in characters. A name longer than this is truncated
    /// rather than refused: the name is decoration, and refusing a perfectly good file over it
    /// would be a baffling failure.
    /// </summary>
    public const int MaxAttachmentFileNameLength = 255;
}
