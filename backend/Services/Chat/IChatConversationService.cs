using JuggerHub.Common;
using JuggerHub.Dtos.Chat;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Conversations: starting them, listing the inbox, membership, read state and per-user flags
/// (feature 019). Every method authorizes server-side through <see cref="ChatGuard"/>; a caller who is
/// not a member gets <see cref="ChatOutcome.NotFound"/>, never a 403 that would confirm existence.
/// </summary>
public interface IChatConversationService
{
    /// <summary>
    /// Start a conversation. One participant ⇒ direct (idempotent — returns the existing conversation
    /// for a pair that already has one, spec FR-008); two or more ⇒ a named group.
    /// </summary>
    Task<ChatResult<ConversationSummaryDto>> StartAsync(
        Guid callerId,
        IReadOnlyList<Guid> participantUserIds,
        string? name,
        CancellationToken ct = default);

    /// <summary>
    /// Send the first message to a player, creating the direct conversation if none exists yet
    /// (feature 022 — lazy DM creation). Returns the (possibly newly created) conversation id and the
    /// sent message. Race-safe: concurrent first sends resolve to a single conversation. Enforces the
    /// block rule at send time — a blocked pair cannot bring a DM into existence.
    /// </summary>
    Task<ChatResult<DirectMessageSentDto>> SendFirstDirectAsync(
        Guid callerId,
        Guid targetUserId,
        string body,
        CancellationToken ct = default);

    /// <summary>
    /// Send the first message to a team's or event's admins, creating the inquiry thread if none exists
    /// yet (feature 027 — contact the admins). <paramref name="kind"/> must be
    /// <see cref="Entities.ConversationKind.TeamInquiry"/> or
    /// <see cref="Entities.ConversationKind.EventInquiry"/>; <paramref name="targetId"/> is the team or
    /// event id. Rejects a caller who already administers the target (FR-002). Race-safe: concurrent
    /// first sends resolve to a single thread per (requester, target).
    /// </summary>
    Task<ChatResult<InquiryMessageSentDto>> SendFirstInquiryAsync(
        Guid callerId,
        Entities.ConversationKind kind,
        Guid targetId,
        string body,
        CancellationToken ct = default);

    /// <summary>
    /// The caller's existing inquiry thread id for a team/event, or null. Never creates anything; used
    /// by the "Contact admins" entry point to reuse a thread rather than open a fresh compose (FR-004).
    /// </summary>
    Task<Guid?> FindInquiryThreadAsync(
        Guid callerId,
        Entities.ConversationKind kind,
        Guid targetId,
        CancellationToken ct = default);

    /// <summary>The caller's inbox, most recently active first. Excludes hidden and blocked-counterpart DMs.</summary>
    Task<PagedResult<ConversationSummaryDto>> GetInboxAsync(
        Guid callerId,
        PaginationRequest pagination,
        CancellationToken ct = default);

    /// <summary>The nav badge total: unread across non-muted, non-hidden conversations.</summary>
    Task<int> GetUnreadTotalAsync(Guid callerId, CancellationToken ct = default);

    /// <summary>One conversation's header/details.</summary>
    Task<ChatResult<ConversationDetailDto>> GetDetailAsync(
        Guid callerId,
        Guid conversationId,
        CancellationToken ct = default);

    /// <summary>A conversation's members. For team/party chats this projects the live roster.</summary>
    Task<ChatResult<PagedResult<MemberDto>>> GetMembersAsync(
        Guid callerId,
        Guid conversationId,
        PaginationRequest pagination,
        CancellationToken ct = default);

    /// <summary>Mark read up to a message. Idempotent, and never moves the marker backwards.</summary>
    Task<ChatResult> MarkReadAsync(
        Guid callerId,
        Guid conversationId,
        Guid lastReadMessageId,
        CancellationToken ct = default);

    /// <summary>
    /// Signal that the caller is typing. Persists nothing — the signal is pushed to the conversation's
    /// other members carrying its own expiry, so it clears itself even if the typist vanishes
    /// (spec FR-020).
    /// </summary>
    Task<ChatResult> SignalTypingAsync(Guid callerId, Guid conversationId, CancellationToken ct = default);

    /// <summary>Add people to a manually-created group. Groups only (spec FR-044).</summary>
    Task<ChatResult> AddMembersAsync(
        Guid callerId,
        Guid conversationId,
        IReadOnlyList<Guid> userIds,
        CancellationToken ct = default);

    /// <summary>
    /// Leave a manually-created group. Team/party chats cannot be left — mute or hide instead
    /// (spec FR-026).
    /// </summary>
    Task<ChatResult> LeaveAsync(Guid callerId, Guid conversationId, CancellationToken ct = default);

    /// <summary>Set the caller's own mute/hide flags. Available for every kind — this is what stands in for "leave" on a team/party chat.</summary>
    Task<ChatResult> PatchStateAsync(
        Guid callerId,
        Guid conversationId,
        bool? isMuted,
        bool? isHidden,
        CancellationToken ct = default);

    /// <summary>
    /// Get or create the chat mirroring a team's roster. Idempotent; the unique filtered index settles
    /// a concurrent double-create. This is what satisfies FR-024's backfill without a migration.
    /// </summary>
    Task<Guid> EnsureForTeamAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>Get or create the chat mirroring a party's roster. Idempotent (see <see cref="EnsureForTeamAsync"/>).</summary>
    Task<Guid> EnsureForPartyAsync(Guid partyId, CancellationToken ct = default);

    /// <summary>
    /// Archive a team's chat before the team row is hard-deleted — a <b>snapshot</b>, not a flag
    /// (data-model R3a).
    /// </summary>
    Task ArchiveForTeamAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>Archive a party's chat before the party row is hard-deleted (data-model R3a).</summary>
    Task ArchiveForPartyAsync(Guid partyId, CancellationToken ct = default);

    /// <summary>
    /// Archive every inquiry thread addressed to an event when it is cancelled (feature 027). Snapshot,
    /// not a flag — see <see cref="ArchiveForTeamAsync"/> (data-model R3a).
    /// </summary>
    Task ArchiveInquiriesForEventAsync(Guid eventId, CancellationToken ct = default);
}
