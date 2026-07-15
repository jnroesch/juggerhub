using JuggerHub.Dtos.Trainings;
using JuggerHub.Entities;

namespace JuggerHub.Services.Trainings;

/// <summary>
/// Single-session reads and admin management for team trainings (feature 018): the session page (member or
/// public-outsider), single-session edit (detaches), skip, cancel, and per-session visibility. Writes are
/// team-admin-gated via <see cref="TrainingGuard"/>; the session page is visible to team members or to any
/// signed-in user on an effectively-public session.
/// </summary>
public interface ITrainingSessionService
{
    Task<TrainingResult<TrainingSessionDetailDto>> GetDetailAsync(Guid sessionId, Guid userId, CancellationToken ct = default);

    Task<TrainingResult<TrainingSessionDetailDto>> EditSingleAsync(Guid sessionId, EditSessionRequest request, Guid userId, CancellationToken ct = default);

    Task<TrainingResult> SkipAsync(Guid sessionId, Guid userId, CancellationToken ct = default);

    Task<TrainingResult<TrainingSessionRowDto>> CancelAsync(Guid sessionId, Guid userId, CancellationToken ct = default);

    Task<TrainingResult> SetSessionVisibilityAsync(Guid sessionId, TrainingVisibility visibility, Guid userId, CancellationToken ct = default);
}
