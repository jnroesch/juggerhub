using JuggerHub.Common;
using JuggerHub.Dtos.Badges;
using JuggerHub.Services.Recognition;

namespace JuggerHub.Services.Badges;

/// <summary>
/// Admin-facing operations on the badge catalog and awards (feature 012, manual awarding only).
/// All callers are already gated by the <c>PlatformAdmin</c> policy at the controller.
/// </summary>
public interface IBadgeService
{
    Task<PagedResult<BadgeDefinitionDto>> ListDefinitionsAsync(
        PaginationRequest pagination, bool includeRetired, CancellationToken ct = default);

    Task<BadgeDefinitionDto> CreateDefinitionAsync(BadgeDefinitionUpsertRequest request, CancellationToken ct = default);

    /// <summary>Returns the updated DTO, or null if no such definition.</summary>
    Task<BadgeDefinitionDto?> UpdateDefinitionAsync(Guid id, BadgeDefinitionUpsertRequest request, CancellationToken ct = default);

    /// <summary>Soft-retire; false if no such definition. Existing awards are preserved.</summary>
    Task<bool> RetireDefinitionAsync(Guid id, CancellationToken ct = default);

    Task<IconOutcome> SetIconAsync(Guid definitionId, byte[] content, CancellationToken ct = default);

    Task<(byte[] Bytes, string ContentType)?> GetIconAsync(Guid definitionId, CancellationToken ct = default);

    Task<(GrantOutcome Outcome, BadgeAwardDto? Award)> GrantAsync(
        Guid definitionId, GrantBadgeRequest request, Guid grantedByUserId, CancellationToken ct = default);

    Task<RevokeOutcome> RevokeAsync(Guid awardId, string? reason, Guid revokedByUserId, CancellationToken ct = default);
}
