using JuggerHub.Common;
using JuggerHub.Dtos.Trainings;
using JuggerHub.Entities;

namespace JuggerHub.Services.Trainings;

/// <summary>
/// RSVP + attendance for team trainings (feature 018): upsert a member's/guest's three-way answer, the
/// admin full attendance list, guest removal, and the cross-team "Your trainings" dashboard agenda. Access
/// is resolved server-side via <see cref="TrainingGuard"/> (member, or outsider-as-guest on a public
/// session).
/// </summary>
public interface ITrainingResponseService
{
    /// <summary>Set/change the caller's answer (upsert). Returns the session row with fresh counts + the caller's answer.</summary>
    Task<TrainingResult<TrainingSessionRowDto>> SetResponseAsync(Guid sessionId, TrainingRsvp answer, Guid userId, CancellationToken ct = default);

    /// <summary>Admin full attendance incl. guests, optionally filtered to one answer group.</summary>
    Task<TrainingResult<PagedResult<AttendanceEntryDto>>> GetAttendanceAsync(Guid sessionId, TrainingRsvp? group, Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Remove a guest's response (never touches team membership). Invalid if the target is a team member.</summary>
    Task<TrainingResult> RemoveGuestAsync(Guid sessionId, Guid targetUserId, Guid userId, CancellationToken ct = default);

    /// <summary>The caller's next upcoming sessions across all their teams + public sessions they joined as a guest.</summary>
    Task<PagedResult<AgendaSessionDto>> GetMyAgendaAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default);
}
