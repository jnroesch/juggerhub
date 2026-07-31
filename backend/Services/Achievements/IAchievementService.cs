using JuggerHub.Common;
using JuggerHub.Dtos.Achievements;
using JuggerHub.Dtos.Recognition;
using JuggerHub.Services.Media;
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

    /// <summary>Un-retire a definition (feature 014); false if no such definition.</summary>
    Task<bool> ReinstateDefinitionAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Normalize an uploaded icon through the image pipeline (#101) and store the resulting WebP.
    /// A rejected upload leaves any existing icon untouched.
    /// </summary>
    Task<IconSetResult> SetIconAsync(Guid definitionId, byte[] content, CancellationToken ct = default);

    /// <summary>Remove a definition's icon (feature 014); false if no such definition. Idempotent if none.</summary>
    Task<bool> RemoveIconAsync(Guid definitionId, CancellationToken ct = default);

    /// <summary>
    /// Open the definition's icon for serving, or null when there is none (or its stored object is
    /// missing). Streams from the media store (feature 035); the caller disposes the stream.
    /// </summary>
    Task<MediaContent?> GetIconAsync(Guid definitionId, CancellationToken ct = default);

    Task<(GrantOutcome Outcome, AchievementAwardDto? Award)> GrantAsync(
        Guid definitionId, GrantAchievementRequest request, Guid grantedByUserId, CancellationToken ct = default);

    Task<RevokeOutcome> RevokeAsync(Guid awardId, string? reason, Guid revokedByUserId, CancellationToken ct = default);

    Task<IReadOnlyList<AdminAwardDto>?> ListPlayerAwardsAsync(string handle, CancellationToken ct = default);

    Task<IReadOnlyList<AdminAwardDto>?> ListTeamAwardsAsync(string slug, CancellationToken ct = default);
}
