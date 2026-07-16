using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Trainings;
using JuggerHub.Entities;
using JuggerHub.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Trainings;

/// <summary>
/// Series/one-off lifecycle for team trainings (feature 018). Create materialises concrete sessions via
/// <see cref="RecurrenceExpander"/>; whole-series edits apply in place for time/place/visibility and
/// regenerate the future set on a pattern/end-date change (see data-model §Reconciliation). Every write is
/// team-admin-gated through <see cref="TrainingGuard"/> (constitution Principle I).
/// </summary>
public sealed class TrainingSeriesService : ITrainingSeriesService
{
    private readonly AppDbContext _db;
    private readonly TrainingGuard _guard;
    private readonly INotificationService _notifications;

    public TrainingSeriesService(AppDbContext db, TrainingGuard guard, INotificationService notifications)
    {
        _db = db;
        _guard = guard;
        _notifications = notifications;
    }

    public async Task<TrainingResult<CreatedTrainingDto>> CreateAsync(
        string slug, CreateTrainingRequest request, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveTeamAsync(slug, userId, ct);
        if (access is null || !access.Value.IsMember)
        {
            return TrainingResult<CreatedTrainingDto>.Fail(TrainingOutcome.NotFound, "No such team.");
        }

        if (!access.Value.IsAdmin)
        {
            return TrainingResult<CreatedTrainingDto>.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can create trainings.");
        }

        var validation = ValidateCreate(request);
        if (validation is not null)
        {
            return TrainingResult<CreatedTrainingDto>.Fail(TrainingOutcome.Invalid, validation);
        }

        var teamId = access.Value.TeamId;
        var training = new Training
        {
            TeamId = teamId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            LocationKind = request.LocationKind,
            Location = request.LocationKind == LocationKind.InPerson ? request.Location!.Trim() : null,
            VirtualLink = request.LocationKind == LocationKind.Virtual ? request.VirtualLink!.Trim() : null,
            IsRecurring = request.IsRecurring,
            Weekday = request.IsRecurring ? request.Weekday : null,
            Interval = request.IsRecurring ? request.Interval : null,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            StartDate = request.StartDate,
            EndDate = request.IsRecurring ? request.EndDate : null,
            Visibility = request.Visibility,
            CreatedByUserId = userId,
        };

        var dates = request.IsRecurring
            ? RecurrenceExpander.Expand(request.StartDate, request.Weekday!.Value, request.Interval!.Value, request.EndDate!.Value)
            : [request.StartDate];

        if (dates.Count == 0)
        {
            return TrainingResult<CreatedTrainingDto>.Fail(TrainingOutcome.Invalid, "That schedule produces no sessions.");
        }

        var sessions = dates.Select(d => new TrainingSession
        {
            Training = training,
            TeamId = teamId,
            SessionDate = d,
            Status = TrainingSessionStatus.Scheduled,
        }).ToList();

        _db.Trainings.Add(training);
        _db.TrainingSessions.AddRange(sessions);
        await _db.SaveChangesAsync(ct);

        var firstSession = sessions.OrderBy(s => s.SessionDate).First();
        await NotifyTeamScheduledAsync(teamId, userId, training, firstSession.Id, ct);

        return TrainingResult<CreatedTrainingDto>.Ok(new CreatedTrainingDto(training.Id, sessions.Count, firstSession.Id));
    }

    public async Task<PagedResult<TrainingSessionRowDto>?> ListSessionsAsync(
        string slug, string window, Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveTeamAsync(slug, userId, ct);
        if (access is null || !access.Value.IsMember)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.TrainingSessions.AsNoTracking()
            .Where(s => s.TeamId == access.Value.TeamId && s.Status != TrainingSessionStatus.Skipped);

        if (!string.Equals(window, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.SessionDate >= today);
        }

        query = query
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTimeOverride ?? s.Training.StartTime);

        return await PageRowsAsync(query, userId, pagination, ct);
    }

    public async Task<TrainingResult<PagedResult<TrainingSeriesSummaryDto>>> ListSeriesAsync(
        string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveTeamAsync(slug, userId, ct);
        if (access is null || !access.Value.IsMember)
        {
            return TrainingResult<PagedResult<TrainingSeriesSummaryDto>>.Fail(TrainingOutcome.NotFound, "No such team.");
        }

        if (!access.Value.IsAdmin)
        {
            return TrainingResult<PagedResult<TrainingSeriesSummaryDto>>.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can view the series overview.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var baseQuery = _db.Trainings.AsNoTracking()
            .Where(t => t.TeamId == access.Value.TeamId && t.IsRecurring)
            .OrderByDescending(t => t.CreatedDate);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .Skip(pagination.NormalizedSkip).Take(pagination.NormalizedTake)
            .Select(t => new TrainingSeriesSummaryDto(
                t.Id, t.Name, t.Weekday, t.Interval, t.StartTime, t.EndTime, t.EndDate, t.Visibility,
                t.Sessions.Count(s => s.Status == TrainingSessionStatus.Scheduled && s.SessionDate >= today),
                t.Sessions.Where(s => s.Status == TrainingSessionStatus.Scheduled && s.SessionDate >= today)
                    .OrderBy(s => s.SessionDate).Select(s => (DateOnly?)s.SessionDate).FirstOrDefault()))
            .ToListAsync(ct);

        return TrainingResult<PagedResult<TrainingSeriesSummaryDto>>.Ok(
            new PagedResult<TrainingSeriesSummaryDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    public async Task<PagedResult<TrainingSessionRowDto>> ListPublicAsync(
        string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var normalized = Teams.TeamSlugPolicy.Normalize(slug);
        var teamId = await _db.Teams.AsNoTracking()
            .Where(t => t.Slug == normalized).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (teamId is null)
        {
            return new PagedResult<TrainingSessionRowDto>([], 0, pagination.NormalizedSkip, pagination.NormalizedTake);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.TrainingSessions.AsNoTracking()
            .Where(s => s.TeamId == teamId
                && s.Status != TrainingSessionStatus.Skipped
                && s.SessionDate >= today
                && (s.VisibilityOverride ?? s.Training.Visibility) == TrainingVisibility.Public)
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTimeOverride ?? s.Training.StartTime);

        return await PageRowsAsync(query, userId, pagination, ct);
    }

    public async Task<TrainingResult<SeriesEditResultDto>> EditSeriesAsync(
        Guid trainingId, EditSeriesRequest request, Guid userId, CancellationToken ct = default)
    {
        var training = await _db.Trainings
            .Include(t => t.Sessions)
            .FirstOrDefaultAsync(t => t.Id == trainingId, ct);
        if (training is null)
        {
            return TrainingResult<SeriesEditResultDto>.Fail(TrainingOutcome.NotFound, "No such training.");
        }

        var access = await _guard.ResolveTeamAsync(await SlugForTeamAsync(training.TeamId, ct), userId, ct);
        if (access is null || !access.Value.IsAdmin)
        {
            return TrainingResult<SeriesEditResultDto>.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can edit a training.");
        }

        // Validate time change.
        var newStart = request.StartTime ?? training.StartTime;
        var newEnd = request.EndTime ?? training.EndTime;
        if (newEnd <= newStart)
        {
            return TrainingResult<SeriesEditResultDto>.Fail(TrainingOutcome.Invalid, "The end time must be after the start time.");
        }

        // In-place template updates (upcoming non-detached inherit automatically).
        if (request.Name is { } name && !string.IsNullOrWhiteSpace(name)) training.Name = name.Trim();
        if (request.Description is not null) training.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (request.LocationKind is { } lk) training.LocationKind = lk;
        if (request.Location is not null) training.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        if (request.VirtualLink is not null) training.VirtualLink = string.IsNullOrWhiteSpace(request.VirtualLink) ? null : request.VirtualLink.Trim();
        if (request.Visibility is { } vis) training.Visibility = vis;
        training.StartTime = newStart;
        training.EndTime = newEnd;

        var added = 0;
        var removed = 0;
        var kept = 0;

        var patternChanged = training.IsRecurring
            && (request.Weekday is not null || request.Interval is not null || request.EndDate is not null);

        if (patternChanged)
        {
            var weekday = request.Weekday ?? training.Weekday!.Value;
            var interval = request.Interval ?? training.Interval!.Value;
            var endDate = request.EndDate ?? training.EndDate!.Value;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var regenFrom = training.StartDate > today ? training.StartDate : today;

            var wanted = RecurrenceExpander.Expand(regenFrom, weekday, interval, endDate).ToHashSet();
            if (wanted.Count == 0)
            {
                return TrainingResult<SeriesEditResultDto>.Fail(TrainingOutcome.Invalid, "That change would leave no upcoming sessions.");
            }

            training.Weekday = weekday;
            training.Interval = interval;
            training.EndDate = endDate;

            var futureNonDetached = training.Sessions
                .Where(s => !s.Detached && s.Status == TrainingSessionStatus.Scheduled && s.SessionDate >= today)
                .ToList();
            var existingDates = futureNonDetached.Select(s => s.SessionDate).ToHashSet();

            foreach (var s in futureNonDetached.Where(s => !wanted.Contains(s.SessionDate)))
            {
                _db.TrainingSessions.Remove(s);
                removed++;
            }

            kept = futureNonDetached.Count(s => wanted.Contains(s.SessionDate));

            foreach (var d in wanted.Where(d => !existingDates.Contains(d)))
            {
                _db.TrainingSessions.Add(new TrainingSession
                {
                    TrainingId = training.Id,
                    TeamId = training.TeamId,
                    SessionDate = d,
                    Status = TrainingSessionStatus.Scheduled,
                });
                added++;
            }
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            kept = training.Sessions.Count(s => !s.Detached && s.Status == TrainingSessionStatus.Scheduled && s.SessionDate >= today);
        }

        await _db.SaveChangesAsync(ct);
        await NotifyRespondersUpdatedAsync(training.Id, training.TeamId, training.Name, "seriesEdit", null, userId, ct);

        return TrainingResult<SeriesEditResultDto>.Ok(new SeriesEditResultDto(training.Id, added, removed, kept));
    }

    public async Task<TrainingResult> SetSeriesVisibilityAsync(
        Guid trainingId, TrainingVisibility visibility, Guid userId, CancellationToken ct = default)
    {
        var training = await _db.Trainings.FirstOrDefaultAsync(t => t.Id == trainingId, ct);
        if (training is null)
        {
            return TrainingResult.Fail(TrainingOutcome.NotFound, "No such training.");
        }

        var access = await _guard.ResolveTeamAsync(await SlugForTeamAsync(training.TeamId, ct), userId, ct);
        if (access is null || !access.Value.IsAdmin)
        {
            return TrainingResult.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can change visibility.");
        }

        training.Visibility = visibility;
        await _db.SaveChangesAsync(ct);
        return TrainingResult.Ok();
    }

    // ---- Helpers -----------------------------------------------------------

    private static string? ValidateCreate(CreateTrainingRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "A name is required.";
        if (r.EndTime <= r.StartTime) return "The end time must be after the start time.";
        if (r.LocationKind == LocationKind.InPerson && string.IsNullOrWhiteSpace(r.Location)) return "A location is required.";
        if (r.LocationKind == LocationKind.Virtual && string.IsNullOrWhiteSpace(r.VirtualLink)) return "A join link is required.";
        if (r.IsRecurring)
        {
            if (r.Weekday is null || r.Interval is null || r.EndDate is null) return "A series needs a weekday, interval and end date.";
            if (r.EndDate < r.StartDate) return "The end date must be on or after the start date.";
        }

        return null;
    }

    private async Task<string> SlugForTeamAsync(Guid teamId, CancellationToken ct) =>
        await _db.Teams.AsNoTracking().Where(t => t.Id == teamId).Select(t => t.Slug).FirstAsync(ct);

    private async Task<PagedResult<TrainingSessionRowDto>> PageRowsAsync(
        IQueryable<TrainingSession> query, Guid userId, PaginationRequest pagination, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(pagination.NormalizedSkip).Take(pagination.NormalizedTake)
            .Select(RowProjection(userId))
            .ToListAsync(ct);
        return new PagedResult<TrainingSessionRowDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    /// <summary>Row projection shared by the tab and public list. Guests count only while the session is effectively public.</summary>
    internal static System.Linq.Expressions.Expression<Func<TrainingSession, TrainingSessionRowDto>> RowProjection(Guid userId) =>
        s => new TrainingSessionRowDto(
            s.Id,
            s.TrainingId,
            s.Training.Name,
            !s.Training.IsRecurring,
            s.SessionDate,
            s.StartTimeOverride ?? s.Training.StartTime,
            s.EndTimeOverride ?? s.Training.EndTime,
            s.LocationKindOverride ?? s.Training.LocationKind,
            (s.LocationKindOverride ?? s.Training.LocationKind) == LocationKind.Virtual ? null : (s.LocationOverride ?? s.Training.Location),
            (s.LocationKindOverride ?? s.Training.LocationKind) == LocationKind.Virtual ? (s.VirtualLinkOverride ?? s.Training.VirtualLink) : null,
            s.VisibilityOverride ?? s.Training.Visibility,
            s.Status,
            s.Responses.Count(r => r.Answer == TrainingRsvp.Going && (!r.IsGuest || (s.VisibilityOverride ?? s.Training.Visibility) == TrainingVisibility.Public)),
            s.Responses.Count(r => r.Answer == TrainingRsvp.Maybe && (!r.IsGuest || (s.VisibilityOverride ?? s.Training.Visibility) == TrainingVisibility.Public)),
            s.Responses.Count(r => r.Answer == TrainingRsvp.Cant && (!r.IsGuest || (s.VisibilityOverride ?? s.Training.Visibility) == TrainingVisibility.Public)),
            s.Responses.Where(r => r.UserId == userId).Select(r => (TrainingRsvp?)r.Answer).FirstOrDefault(),
            s.Detached);

    private async Task NotifyTeamScheduledAsync(Guid teamId, Guid actorId, Training training, Guid firstSessionId, CancellationToken ct)
    {
        var slug = await SlugForTeamAsync(teamId, ct);
        var recipients = await _db.TeamMemberships.AsNoTracking()
            .Where(m => m.TeamId == teamId && m.UserId != actorId)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        if (recipients.Count == 0)
        {
            return;
        }

        var payload = new { teamSlug = slug, trainingId = training.Id, trainingName = training.Name, sessionId = firstSessionId, isRecurring = training.IsRecurring };
        await _notifications.CreateManyAsync(recipients, NotificationType.TrainingScheduled, payload, actorId, $"training-scheduled:{training.Id}", ct);
    }

    private async Task NotifyRespondersUpdatedAsync(
        Guid trainingId, Guid teamId, string trainingName, string kind, Guid? sessionId, Guid actorId, CancellationToken ct)
    {
        var slug = await SlugForTeamAsync(teamId, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var responders = await _db.TrainingResponses.AsNoTracking()
            .Where(r => r.Session.TrainingId == trainingId
                && r.Session.Status == TrainingSessionStatus.Scheduled
                && r.Session.SessionDate >= today
                && r.UserId != actorId)
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(ct);
        if (responders.Count == 0)
        {
            return;
        }

        var payload = new { teamSlug = slug, trainingId, sessionId, trainingName, kind };
        await _notifications.CreateManyAsync(responders, NotificationType.TrainingUpdated, payload, actorId, $"training-updated:{trainingId}:{kind}", ct);
    }
}
