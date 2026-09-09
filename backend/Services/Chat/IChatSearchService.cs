using JuggerHub.Common;
using JuggerHub.Dtos.Chat;

namespace JuggerHub.Services.Chat;

/// <summary>
/// People to start a chat with (feature 019, User Story 6). Since feature 046 this is people only —
/// message text is not searched anywhere in the product; the inbox finds conversations by name via
/// <see cref="IChatConversationService.GetInboxAsync"/>.
/// </summary>
public interface IChatSearchService
{
    /// <summary>
    /// Search players by display name or handle: open reach (019 FR-049), excluding the caller and
    /// anyone blocked in either direction (FR-033). A term shorter than the minimum yields an empty
    /// result, never an error.
    /// </summary>
    Task<ChatSearchResultDto> SearchAsync(
        Guid callerId,
        string term,
        PaginationRequest pagination,
        CancellationToken ct = default);
}
