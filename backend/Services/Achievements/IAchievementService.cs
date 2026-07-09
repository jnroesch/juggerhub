using JuggerHub.Common;
using JuggerHub.Dtos.Achievements;
using JuggerHub.Services.Recognition;

namespace JuggerHub.Services.Achievements;

/// <summary>
/// Admin-facing operations on the achievement catalog and awards (feature 012, manual awarding
/// only). Parallel to <see cref="Badges.IBadgeService"/>; achievement awards additionally carry
/// optional accomplishment context. Callers are gated by the <c>PlatformAdmin</c> policy.
/// </summary>
public interface IAchievementService
{
    Task<PagedResult<AchievementDefinitionDto>> ListDefinitionsAsync(
        PaginationRequest pagination, bool includeRetired, CancellationToken ct = default);

    Task<AchievementDefinitionDto> CreateDefinitionAsync(AchievementDefinitionUpsertRequest request, CancellationToken ct = default);

    Task<AchievementDefinitionDto?> UpdateDefinitionAsync(Guid id, AchievementDefinitionUpsertRequest request, CancellationToken ct = default);

    Task<bool> RetireDefinitionAsync(Guid id, CancellationToken ct = default);

    Task<IconOutcome> SetIconAsync(Guid definitionId, byte[] content, CancellationToken ct = default);

    Task<(byte[] Bytes, string ContentType)?> GetIconAsync(Guid definitionId, CancellationToken ct = default);

    Task<(GrantOutcome Outcome, AchievementAwardDto? Award)> GrantAsync(
        Guid definitionId, GrantAchievementRequest request, Guid grantedByUserId, CancellationToken ct = default);

    Task<RevokeOutcome> RevokeAsync(Guid awardId, string? reason, Guid revokedByUserId, CancellationToken ct = default);
}
