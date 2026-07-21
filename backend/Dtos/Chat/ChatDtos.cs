using JuggerHub.Entities;

namespace JuggerHub.Dtos.Chat;

// Client-facing shapes for chat (feature 019). See specs/019-chat/contracts/chat-api.md.
// Services return entities; controllers map to these with Mapster (constitution Principle II).

/// <summary>How a conversation is pictured in the inbox: a player's avatar, a team/party crest, or a group cluster.</summary>
public sealed record ConversationAvatarDto(string Kind, Guid? UserId, Guid? TeamId, string? Url);

/// <summary>The inbox row's last-line preview. Empty text when the newest message was deleted.</summary>
public sealed record LastMessageDto(
    string Preview,
    DateTime At,
    string? SenderName,
    bool IsOwn,
    bool IsSystem);

/// <summary>One inbox row.</summary>
public sealed record ConversationSummaryDto(
    Guid Id,
    ConversationKind Kind,
    string Name,
    ConversationAvatarDto Avatar,
    LastMessageDto? LastMessage,
    int UnreadCount,
    bool IsMuted,
    ConversationState State,
    Guid? TeamId,
    Guid? PartyId);

/// <summary>A conversation's header/details, including the viewer's own per-conversation flags.</summary>
public sealed record ConversationDetailDto(
    Guid Id,
    ConversationKind Kind,
    string Name,
    ConversationAvatarDto Avatar,
    ConversationState State,
    bool IsMuted,
    bool IsHidden,
    int MemberCount,
    bool CanLeave,
    bool CanAddMembers,
    Guid? TeamId,
    Guid? PartyId);

/// <summary>One member in a conversation's member list.</summary>
public sealed record MemberDto(
    Guid UserId,
    string DisplayName,
    string? Handle,
    string? AvatarUrl,
    bool IsYou,
    bool ViaMarket);

/// <summary>
/// A view-only card for a JuggerHub item a message linked to. Resolved per <em>viewer</em> — a viewer
/// without permission to see the target gets no card at all and just reads the link in the body
/// (spec FR-040). Carries no actions: acting means following <see cref="Href"/> (spec FR-038).
/// </summary>
public sealed record LinkCardDto(
    ChatLinkKind Kind,
    Guid TargetId,
    string Title,
    string? Subtitle,
    string Href,
    string? AvatarUrl);

/// <summary>One message in a thread.</summary>
public sealed record MessageDto(
    Guid Id,
    ChatMessageKind Kind,
    Guid? SenderId,
    string? SenderName,
    bool IsOwn,
    string Body,
    DateTime SentAt,
    bool IsDeleted,
    string? ReadState,
    ChatSystemEvent? SystemEvent,
    string? SystemSubjectName,
    LinkCardDto? LinkCard);

/// <summary>
/// A keyset page of history, newest first. <see cref="NextBefore"/> is the cursor for the next page
/// back, or null when the history is exhausted. Keyset rather than skip/take because a chat is the
/// textbook large, rapidly-changing table (constitution Principle III).
/// </summary>
public sealed record MessagePageDto(IReadOnlyList<MessageDto> Items, Guid? NextBefore);

/// <summary>One hit when searching your own messages.</summary>
public sealed record MessageSearchHitDto(
    Guid MessageId,
    Guid ConversationId,
    string ConversationName,
    ConversationKind ConversationKind,
    string Snippet,
    DateTime SentAt,
    string? SenderName);

/// <summary>One hit when searching for people to chat with.</summary>
public sealed record PersonHitDto(
    Guid UserId,
    string DisplayName,
    string? Handle,
    string? AvatarUrl,
    Guid? ExistingConversationId);

/// <summary>Search results, split the way the inbox renders them: your messages, and people.</summary>
public sealed record ChatSearchResultDto(
    Common.PagedResult<MessageSearchHitDto> Messages,
    Common.PagedResult<PersonHitDto> People);

/// <summary>A player you have blocked.</summary>
public sealed record BlockedUserDto(Guid UserId, string DisplayName, string? Handle, DateTime BlockedAt);

/// <summary>The nav badge's number.</summary>
public sealed record UnreadCountDto(int UnreadCount);

/// <summary>
/// Result of sending the first message to a player (feature 022 — lazy DM creation): the direct
/// conversation that now exists (created if it didn't) plus the message that was sent, so the client
/// can navigate into the real thread.
/// </summary>
public sealed record DirectMessageSentDto(Guid ConversationId, MessageDto Message);

// --- Requests -------------------------------------------------------------------------------

/// <summary>Start a chat: exactly one participant ⇒ a direct conversation; two or more ⇒ a named group.</summary>
public sealed record CreateConversationRequest(IReadOnlyList<Guid> ParticipantUserIds, string? Name);

/// <summary>Add people to a manually-created group.</summary>
public sealed record AddMembersRequest(IReadOnlyList<Guid> UserIds);

/// <summary>Send a text message.</summary>
public sealed record SendMessageRequest(string Body);

/// <summary>Mark read up to (and including) a message.</summary>
public sealed record MarkReadRequest(Guid LastReadMessageId);

/// <summary>Change the viewer's own per-conversation flags. Either field may be omitted.</summary>
public sealed record PatchConversationStateRequest(bool? IsMuted, bool? IsHidden);

/// <summary>Block a player.</summary>
public sealed record BlockUserRequest(Guid UserId);
