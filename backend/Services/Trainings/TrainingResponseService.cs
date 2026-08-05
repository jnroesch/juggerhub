using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Trainings;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Trainings;

/// <summary>
/// RSVP + attendance for team trainings (feature 018). A member or an outsider-on-a-public-session upserts
/// one three-way answer (the outsider recorded as a guest); admins read full attendance and remove guests;
/// the dashboard agenda merges the caller's upcoming sessions across every team plus public sessions they
/// joined as a guest. Access is resolved server-side via <see cref="TrainingGuard"/>.
/// </summary>
public sealed class TrainingResponseService : ITrainingResponseService
{
    private readonly AppDbContext _db;
    private readonly TrainingGuard _guard;

    public TrainingResponseService(AppDbContext db, TrainingGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    public async Task<TrainingResult<TrainingSessionRowDto>> SetResponseAsync(
        Guid sessionId, TrainingRsvp answer, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveSessionAsync(sessionId, userId, ct);
        if (access is null || !access.Value.CanView)
        {
            return TrainingResult<TrainingSessionRowDto>.Fail(TrainingOutcome.NotFound, "No such session.");
        }

        if (access.Value.Status != TrainingSessionStatus.Scheduled || access.Value.IsPast)
        {
            return TrainingResult<TrainingSessionRowDto>.Fail(TrainingOutcome.Conflict, "This session is no longer open for responses.");
        }

        var existing = await _db.TrainingResponses
            .FirstOrDefaultAsync(r => r.TrainingSessionId == sessionId && r.UserId == userId, ct);
        if (existing is null)
        {
            _db.TrainingResponses.Add(new TrainingResponse
            {
                TrainingSessionId = sessionId,
                UserId = userId,
                Answer = answer,
                IsGuest = !access.Value.IsTeamMember,
            });
        }
        else
        {
            existing.Answer = answer;
            existing.IsGuest = !access.Value.IsTeamMember;
        }

        await _db.SaveChangesAsync(ct);

        var raw = await _db.TrainingSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(TrainingSeriesService.RowProjection(userId))
            .FirstAsync(ct);
        return TrainingResult<TrainingSessionRowDto>.Ok(TrainingSeriesService.ToRow(raw));
    }

    public async Task<TrainingResult<PagedResult<AttendanceEntryDto>>> GetAttendanceAsync(
        Guid sessionId, TrainingRsvp? group, Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveSessionAsync(sessionId, userId, ct);
        if (access is null)
        {
            return TrainingResult<PagedResult<AttendanceEntryDto>>.Fail(TrainingOutcome.NotFound, "No such session.");
        }

        if (!access.Value.IsAdmin)
        {
            return TrainingResult<PagedResult<AttendanceEntryDto>>.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can view attendance.");
        }

        var isPublic = access.Value.EffectiveVisibility == TrainingVisibility.Public;
        var teamId = access.Value.TeamId;
        var query = _db.TrainingResponses.AsNoTracking()
            .Where(r => r.TrainingSessionId == sessionId && (!r.IsGuest || isPublic));
        if (group is { } g)
        {
            query = query.Where(r => r.Answer == g);
        }

        var ordered = query
            .OrderBy(r => r.Answer)
            .ThenByDescending(r => r.UserId == userId)
            .ThenBy(r => r.User.Profile!.DisplayName);

        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip(pagination.NormalizedSkip).Take(pagination.NormalizedTake)
            .Select(r => new AttendanceEntryDto(
                r.UserId,
                r.User.Profile!.Handle,
                r.User.Profile!.DisplayName,
                r.User.Profile!.Avatar != null,
                r.User.Profile!.Pompfen.Select(p => p.Pompfe).Cast<Pompfe?>().FirstOrDefault().ToString(),
                r.IsGuest,
                r.UserId == userId,
                _db.TeamMemberships.Any(m => m.TeamId == teamId && m.UserId == r.UserId && m.Role == TeamRole.Admin),
                r.Answer))
            .ToListAsync(ct);

        return TrainingResult<PagedResult<AttendanceEntryDto>>.Ok(
            new PagedResult<AttendanceEntryDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    public async Task<TrainingResult> RemoveGuestAsync(Guid sessionId, Guid targetUserId, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveSessionAsync(sessionId, userId, ct);
        if (access is null)
        {
            return TrainingResult.Fail(TrainingOutcome.NotFound, "No such session.");
        }

        if (!access.Value.IsAdmin)
        {
            return TrainingResult.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can remove a guest.");
        }

        var isMember = await _db.TeamMemberships.AsNoTracking()
            .AnyAsync(m => m.TeamId == access.Value.TeamId && m.UserId == targetUserId, ct);
        if (isMember)
        {
            return TrainingResult.Fail(TrainingOutcome.Invalid, "That person is a team member, not a guest.");
        }

        var deleted = await _db.TrainingResponses
            .Where(r => r.TrainingSessionId == sessionId && r.UserId == targetUserId && r.IsGuest)
            .ExecuteDeleteAsync(ct);
        return deleted == 0
            ? TrainingResult.Fail(TrainingOutcome.NotFound, "No such guest on this session.")
            : TrainingResult.Ok();
    }

    public async Task<PagedResult<AgendaSessionDto>> GetMyAgendaAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.TrainingSessions.AsNoTracking()
            .Where(s => s.Status != TrainingSessionStatus.Skipped
                && s.SessionDate >= today
                && (s.Training.Team.Memberships.Any(m => m.UserId == userId)
                    || ((s.VisibilityOverride ?? s.Training.Visibility) == TrainingVisibility.Public
                        && s.Responses.Any(r => r.UserId == userId && r.IsGuest))))
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTimeOverride ?? s.Training.StartTime);

        var total = await query.CountAsync(ct);
        // The label is composed after materialization so HomeProjections.LocationLabel stays the
        // single implementation of the city → venue → legacy rule (042 SC-003); it is a C# method
        // and cannot be translated to SQL. The address itself resolves as an indivisible block
        // keyed on CityIdOverride — never per-field `??` (042 research R1).
        var raw = await query
            .Skip(pagination.NormalizedSkip).Take(pagination.NormalizedTake)
            .Select(s => new
            {
                s.Id,
                s.TrainingId,
                s.Training.Name,
                IsOneOff = !s.Training.IsRecurring,
                s.SessionDate,
                StartTime = s.StartTimeOverride ?? s.Training.StartTime,
                EndTime = s.EndTimeOverride ?? s.Training.EndTime,
                Kind = s.LocationKindOverride ?? s.Training.LocationKind,
                CityName = s.CityIdOverride != null
                    ? (s.CityOverride != null ? s.CityOverride.Name : null)
                    : (s.Training.City != null ? s.Training.City.Name : null),
                VenueName = s.CityIdOverride != null ? s.VenueNameOverride : s.Training.VenueName,
                LegacyLocation = s.CityIdOverride != null ? s.LocationOverride : s.Training.Location,
                VirtualLink = s.VirtualLinkOverride ?? s.Training.VirtualLink,
                Visibility = s.VisibilityOverride ?? s.Training.Visibility,
                s.Status,
                GoingCount = s.Responses.Count(r => r.Answer == TrainingRsvp.Going && (!r.IsGuest || (s.VisibilityOverride ?? s.Training.Visibility) == TrainingVisibility.Public)),
                MyAnswer = s.Responses.Where(r => r.UserId == userId).Select(r => (TrainingRsvp?)r.Answer).FirstOrDefault(),
                TeamSlug = s.Training.Team.Slug,
                TeamName = s.Training.Team.Name,
                IsPublicGuest = !s.Training.Team.Memberships.Any(m => m.UserId == userId),
            })
            .ToListAsync(ct);

        var items = raw.Select(s => new AgendaSessionDto(
            s.Id,
            s.TrainingId,
            s.Name,
            s.IsOneOff,
            s.SessionDate,
            s.StartTime,
            s.EndTime,
            s.Kind,
            TrainingSeriesService.LocationLabelFor(s.Kind, s.CityName, s.VenueName, s.LegacyLocation),
            s.Kind == LocationKind.Virtual ? s.VirtualLink : null,
            s.Visibility,
            s.Status,
            s.GoingCount,
            s.MyAnswer,
            s.TeamSlug,
            s.TeamName,
            s.IsPublicGuest)).ToList();

        return new PagedResult<AgendaSessionDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }
}
