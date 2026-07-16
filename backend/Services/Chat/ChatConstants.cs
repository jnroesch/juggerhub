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
}
