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
}
