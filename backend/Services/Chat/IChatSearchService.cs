using JuggerHub.Common;
using JuggerHub.Dtos.Chat;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Search from the inbox (feature 019, User Story 6): messages from the caller's own conversations,
/// and people to start a chat with.
/// </summary>
public interface IChatSearchService
{
    /// <summary>
    /// Search. Message results are scoped to the caller's own conversations <b>in the query itself</b>
    /// — never post-filtered (spec FR-035).
    /// </summary>
    Task<ChatSearchResultDto> SearchAsync(
        Guid callerId,
        string term,
        PaginationRequest pagination,
        CancellationToken ct = default);
}
