using JuggerHub.Common;
using JuggerHub.Dtos.Chat;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Blocking (feature 019, User Story 5). The recourse against unwanted direct messages — and
/// load-bearing rather than a nicety, because DM reach is deliberately open (spec FR-049).
/// </summary>
public interface IChatBlockService
{
    /// <summary>The players the caller has blocked.</summary>
    Task<PagedResult<BlockedUserDto>> ListAsync(Guid callerId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Block a player. Idempotent; blocking yourself is rejected.</summary>
    Task<ChatResult> BlockAsync(Guid callerId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>Unblock a player. Direct messaging resumes with the prior history intact (spec FR-030).</summary>
    Task<ChatResult> UnblockAsync(Guid callerId, Guid targetUserId, CancellationToken ct = default);
}
