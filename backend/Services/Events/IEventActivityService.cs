using JuggerHub.Common;
using JuggerHub.Dtos.Profile;

namespace JuggerHub.Services.Events;

/// <summary>
/// Derives a player's recent activity from the minimal events model
/// (participations joined to events), newest-first and always bounded
/// (constitution Principle III — no unbounded lists).
/// </summary>
public interface IEventActivityService
{
    /// <summary>Paged recent activity for a profile, ordered by event date desc.</summary>
    Task<PagedResult<ActivityItemDto>> GetRecentAsync(Guid profileId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>A small capped slice for embedding in a profile payload (newest-first).</summary>
    Task<IReadOnlyList<ActivityItemDto>> GetRecentCappedAsync(Guid profileId, int take, CancellationToken ct = default);
}
