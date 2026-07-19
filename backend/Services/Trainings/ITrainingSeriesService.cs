using JuggerHub.Common;
using JuggerHub.Dtos.Trainings;
using JuggerHub.Entities;

namespace JuggerHub.Services.Trainings;

/// <summary>
/// Series/one-off lifecycle for team trainings (feature 018): create, the Trainings-tab session list, the
/// admin active-series overview, the outsider public list, whole-series edits (in-place + regenerate), and
/// series-level visibility. Every write is team-admin-gated via <see cref="TrainingGuard"/>.
/// </summary>
public interface ITrainingSeriesService
{
    Task<TrainingResult<CreatedTrainingDto>> CreateAsync(string slug, CreateTrainingRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>The Trainings-tab dated list. Null when the caller is not a team member (controller → 404).</summary>
    Task<PagedResult<TrainingSessionRowDto>?> ListSessionsAsync(string slug, string window, Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Admin-only active-series overview.</summary>
    Task<TrainingResult<PagedResult<TrainingSeriesSummaryDto>>> ListSeriesAsync(string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The team's public upcoming sessions, for outsiders (any signed-in user).</summary>
    Task<PagedResult<TrainingSessionRowDto>> ListPublicAsync(string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    Task<TrainingResult<SeriesEditResultDto>> EditSeriesAsync(Guid trainingId, EditSeriesRequest request, Guid userId, CancellationToken ct = default);

    Task<TrainingResult> SetSeriesVisibilityAsync(Guid trainingId, TrainingVisibility visibility, Guid userId, CancellationToken ct = default);
}
