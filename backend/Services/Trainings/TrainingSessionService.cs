using JuggerHub.Data;
using JuggerHub.Dtos.Trainings;
using JuggerHub.Entities;
using JuggerHub.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Trainings;

/// <summary>
/// Single-session reads and admin management for team trainings (feature 018). The session page is visible
/// to team members or to any signed-in user on an effectively-public session; edit (detaches), skip,
/// cancel and per-session visibility are team-admin-gated via <see cref="TrainingGuard"/>. Cancelling
/// notifies that session's responders.
/// </summary>
public sealed class TrainingSessionService : ITrainingSessionService
{
    /// <summary>How many people to surface per who's-coming group (avatars); full list via attendance.</summary>
    private const int WhosComingTop = 12;

    private readonly AppDbContext _db;
    private readonly TrainingGuard _guard;
    private readonly INotificationService _notifications;
    private readonly Geocoding.ICityService _cities;

    public TrainingSessionService(
        AppDbContext db, TrainingGuard guard, INotificationService notifications,
        Geocoding.ICityService cities)
    {
        _db = db;
        _guard = guard;
        _notifications = notifications;
        _cities = cities;
    }

    public async Task<TrainingResult<TrainingSessionDetailDto>> GetDetailAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveSessionAsync(sessionId, userId, ct);
        if (access is null || !access.Value.CanView)
        {
            return TrainingResult<TrainingSessionDetailDto>.Fail(TrainingOutcome.NotFound, "No such session.");
        }

        var dto = await ProjectDetailAsync(sessionId, userId, access.Value, ct);
        return dto is null
            ? TrainingResult<TrainingSessionDetailDto>.Fail(TrainingOutcome.NotFound, "No such session.")
            : TrainingResult<TrainingSessionDetailDto>.Ok(dto);
    }

    public async Task<TrainingResult<TrainingSessionDetailDto>> EditSingleAsync(
        Guid sessionId, EditSessionRequest request, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveSessionAsync(sessionId, userId, ct);
        if (access is null)
        {
            return TrainingResult<TrainingSessionDetailDto>.Fail(TrainingOutcome.NotFound, "No such session.");
        }

        if (!access.Value.IsAdmin)
        {
            return TrainingResult<TrainingSessionDetailDto>.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can edit a session.");
        }

        if (access.Value.IsPast || access.Value.Status != TrainingSessionStatus.Scheduled)
        {
            return TrainingResult<TrainingSessionDetailDto>.Fail(TrainingOutcome.Conflict, "That session can no longer be edited.");
        }

        var session = await _db.TrainingSessions.Include(s => s.Training).FirstAsync(s => s.Id == sessionId, ct);

        var newStart = request.StartTime ?? session.StartTimeOverride ?? session.Training.StartTime;
        var newEnd = request.EndTime ?? session.EndTimeOverride ?? session.Training.EndTime;
        if (newEnd <= newStart)
        {
            return TrainingResult<TrainingSessionDetailDto>.Fail(TrainingOutcome.Invalid, "The end time must be after the start time.");
        }

        var newKind = request.LocationKind ?? session.LocationKindOverride ?? session.Training.LocationKind;

        // Relocating the session: the address is validated and stored as ONE block (042 FR-006/FR-007).
        // Resolve the city before assigning anything below — ResolveAndUpsertAsync owns its own
        // SaveChanges, which would commit a half-applied edit this method might still reject.
        Geocoding.StructuredAddress.CityResult? relocation = null;
        Geocoding.StructuredAddress.AddressResult? relocationAddress = null;
        if (request.Location is not null || request.LocationKind is not null || request.VirtualLink is not null)
        {
            var city = await Geocoding.StructuredAddress.ResolveCityAsync(
                _cities, newKind, request.Location, "training", ct);
            if (city.Reason is not null)
            {
                return TrainingResult<TrainingSessionDetailDto>.Fail(TrainingOutcome.Invalid, city.Reason);
            }

            var address = Geocoding.StructuredAddress.Resolve(
                newKind, request.VenueName, request.Street, request.PostalCode,
                request.VirtualLink ?? session.VirtualLinkOverride ?? session.Training.VirtualLink,
                "training");
            if (address.Reason is not null)
            {
                return TrainingResult<TrainingSessionDetailDto>.Fail(TrainingOutcome.Invalid, address.Reason);
            }

            relocation = city;
            relocationAddress = address;
        }

        // Detaching freezes the session's whole schedule/place: snapshot every currently-inherited field
        // into its override so a later whole-series edit can no longer move it (spec FR-016). Visibility is
        // deliberately excluded — it follows the series unless a per-session visibility toggle overrides it.
        //
        // The address block is frozen the same way (042). Consequence, and intended: after a TIME-ONLY
        // single-session edit, CityIdOverride is non-null even though the admin never touched the
        // address. The session is detached; its address is now genuinely its own.
        session.StartTimeOverride ??= session.Training.StartTime;
        session.EndTimeOverride ??= session.Training.EndTime;
        session.LocationKindOverride ??= session.Training.LocationKind;
        session.LocationOverride ??= session.Training.Location;
        session.VirtualLinkOverride ??= session.Training.VirtualLink;
        if (session.CityIdOverride is null)
        {
            session.VenueNameOverride ??= session.Training.VenueName;
            session.StreetOverride ??= session.Training.Street;
            session.PostalCodeOverride ??= session.Training.PostalCode;
            session.CityIdOverride = session.Training.CityId;
        }

        if (request.SessionDate is { } d) session.SessionDate = d;
        if (request.StartTime is { } st) session.StartTimeOverride = st;
        if (request.EndTime is { } et) session.EndTimeOverride = et;
        if (request.LocationKind is { } lk) session.LocationKindOverride = lk;

        if (relocation is { } city2 && relocationAddress is { } address2)
        {
            session.VenueNameOverride = address2.VenueName;
            session.StreetOverride = address2.Street;
            session.PostalCodeOverride = address2.PostalCode;
            session.CityIdOverride = city2.CityId;
            session.CityOverride = city2.City;
            session.VirtualLinkOverride = address2.VirtualLink;
            session.LocationOverride = TrainingSeriesService.LegacyLocationLabel(newKind, city2.City);
        }

        // FR-003: a session whose effective kind is virtual carries no address at all. Without this
        // the frozen block above would sit on a virtual session forever.
        if ((session.LocationKindOverride ?? session.Training.LocationKind) == LocationKind.Virtual)
        {
            session.VenueNameOverride = null;
            session.StreetOverride = null;
            session.PostalCodeOverride = null;
            session.CityIdOverride = null;
            session.CityOverride = null;
            session.LocationOverride = null;
        }

        session.Detached = true;

        await _db.SaveChangesAsync(ct);

        var refreshed = await _guard.ResolveSessionAsync(sessionId, userId, ct);
        var dto = await ProjectDetailAsync(sessionId, userId, refreshed!.Value, ct);
        return TrainingResult<TrainingSessionDetailDto>.Ok(dto!);
    }

    public async Task<TrainingResult> SkipAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveSessionAsync(sessionId, userId, ct);
        if (access is null)
        {
            return TrainingResult.Fail(TrainingOutcome.NotFound, "No such session.");
        }

        if (!access.Value.IsAdmin)
        {
            return TrainingResult.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can skip a session.");
        }

        if (access.Value.IsPast || access.Value.Status != TrainingSessionStatus.Scheduled)
        {
            return TrainingResult.Fail(TrainingOutcome.Conflict, "That session can no longer be skipped.");
        }

        await _db.TrainingSessions.Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, TrainingSessionStatus.Skipped)
                .SetProperty(s => s.ModifiedDate, DateTime.UtcNow), ct);
        return TrainingResult.Ok();
    }

    public async Task<TrainingResult<TrainingSessionRowDto>> CancelAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveSessionAsync(sessionId, userId, ct);
        if (access is null)
        {
            return TrainingResult<TrainingSessionRowDto>.Fail(TrainingOutcome.NotFound, "No such session.");
        }

        if (!access.Value.IsAdmin)
        {
            return TrainingResult<TrainingSessionRowDto>.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can cancel a session.");
        }

        if (access.Value.IsPast || access.Value.Status != TrainingSessionStatus.Scheduled)
        {
            return TrainingResult<TrainingSessionRowDto>.Fail(TrainingOutcome.Conflict, "That session can no longer be cancelled.");
        }

        await _db.TrainingSessions.Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, TrainingSessionStatus.Cancelled)
                .SetProperty(s => s.CancelledDate, DateTime.UtcNow)
                .SetProperty(s => s.ModifiedDate, DateTime.UtcNow), ct);

        await NotifySessionCancelledAsync(sessionId, access.Value, userId, ct);

        var raw = await _db.TrainingSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(TrainingSeriesService.RowProjection(userId))
            .FirstAsync(ct);
        return TrainingResult<TrainingSessionRowDto>.Ok(TrainingSeriesService.ToRow(raw));
    }

    public async Task<TrainingResult> SetSessionVisibilityAsync(Guid sessionId, TrainingVisibility visibility, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveSessionAsync(sessionId, userId, ct);
        if (access is null)
        {
            return TrainingResult.Fail(TrainingOutcome.NotFound, "No such session.");
        }

        if (!access.Value.IsAdmin)
        {
            return TrainingResult.Fail(TrainingOutcome.NotTeamAdmin, "Only a team admin can change visibility.");
        }

        await _db.TrainingSessions.Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.VisibilityOverride, visibility)
                .SetProperty(s => s.ModifiedDate, DateTime.UtcNow), ct);
        return TrainingResult.Ok();
    }

    // ---- Helpers -----------------------------------------------------------

    private async Task<TrainingSessionDetailDto?> ProjectDetailAsync(
        Guid sessionId, Guid userId, TrainingSessionAccess access, CancellationToken ct)
    {
        var head = await _db.TrainingSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new
            {
                s.TrainingId,
                TeamName = s.Training.Team.Name,
                s.Training.Name,
                s.Training.Description,
                IsOneOff = !s.Training.IsRecurring,
                s.SessionDate,
                StartTime = s.StartTimeOverride ?? s.Training.StartTime,
                EndTime = s.EndTimeOverride ?? s.Training.EndTime,
                EffectiveKind = s.LocationKindOverride ?? s.Training.LocationKind,
                // ⚠ The address is ONE indivisible block keyed on CityIdOverride — never per-field
                // `?? Training.X`, which would mix a session's street with the series' city or leak
                // the series' venue name (042 research R1 / TrainingSession remarks).
                VenueName = s.CityIdOverride != null ? s.VenueNameOverride : s.Training.VenueName,
                Street = s.CityIdOverride != null ? s.StreetOverride : s.Training.Street,
                PostalCode = s.CityIdOverride != null ? s.PostalCodeOverride : s.Training.PostalCode,
                City = s.CityIdOverride != null ? s.CityOverride : s.Training.City,
                LegacyLocation = s.CityIdOverride != null ? s.LocationOverride : s.Training.Location,
                VirtualLink = s.VirtualLinkOverride ?? s.Training.VirtualLink,
                s.Training.Weekday,
                s.Training.Interval,
                s.Training.EndDate,
                s.Training.IsRecurring,
                s.Detached,
                MyAnswer = s.Responses.Where(r => r.UserId == userId).Select(r => (TrainingRsvp?)r.Answer).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        if (head is null)
        {
            return null;
        }

        var whosComing = await BuildWhosComingAsync(sessionId, userId, access, ct);

        return new TrainingSessionDetailDto(
            sessionId,
            head.TrainingId,
            access.TeamSlug,
            head.TeamName,
            head.Name,
            head.Description,
            head.IsOneOff,
            head.SessionDate,
            head.StartTime,
            head.EndTime,
            head.EffectiveKind,
            head.EffectiveKind == LocationKind.Virtual ? null : head.VenueName,
            head.EffectiveKind == LocationKind.Virtual ? null : head.Street,
            head.EffectiveKind == LocationKind.Virtual ? null : head.PostalCode,
            head.EffectiveKind == LocationKind.Virtual ? null : Geocoding.LocationLabels.ToLocation(head.City),
            TrainingSeriesService.LocationLabelFor(
                head.EffectiveKind,
                head.City?.Name,
                head.VenueName,
                head.LegacyLocation),
            head.EffectiveKind == LocationKind.Virtual ? head.VirtualLink : null,
            head.IsRecurring ? SeriesLabel(head.Interval) : null,
            head.Weekday,
            head.Interval,
            head.EndDate,
            access.EffectiveVisibility,
            access.Status,
            access.IsPast,
            head.Detached,
            access.IsAdmin,
            access.IsGuest,
            head.MyAnswer,
            whosComing);
    }

    private async Task<WhosComingDto> BuildWhosComingAsync(
        Guid sessionId, Guid userId, TrainingSessionAccess access, CancellationToken ct)
    {
        var isPublic = access.EffectiveVisibility == TrainingVisibility.Public;
        var rows = await _db.TrainingResponses.AsNoTracking()
            .Where(r => r.TrainingSessionId == sessionId && (!r.IsGuest || isPublic))
            .Select(r => new
            {
                r.Answer,
                r.IsGuest,
                r.UserId,
                Handle = r.User.Profile!.Handle,
                DisplayName = r.User.Profile!.DisplayName,
                // Whether a picture exists at all — without it the template can only guess, and a
                // guess renders a broken <img> for every player who never uploaded one.
                HasAvatar = r.User.Profile!.Avatar != null,
                FirstPompfe = r.User.Profile!.Pompfen.Select(p => (Pompfe?)p.Pompfe).FirstOrDefault(),
            })
            .ToListAsync(ct);

        WhosComingGroupDto Group(TrainingRsvp answer)
        {
            var members = rows.Where(x => x.Answer == answer).ToList();
            var people = members
                .OrderByDescending(x => x.UserId == userId)
                .ThenBy(x => x.DisplayName)
                .Take(WhosComingTop)
                .Select(x => new WhosComingPersonDto(
                    x.Handle, x.DisplayName, x.HasAvatar, x.FirstPompfe?.ToString(), x.IsGuest, x.UserId == userId))
                .ToList();
            return new WhosComingGroupDto(members.Count, people);
        }

        return new WhosComingDto(Group(TrainingRsvp.Going), Group(TrainingRsvp.Maybe), Group(TrainingRsvp.Cant));
    }

    private async Task NotifySessionCancelledAsync(Guid sessionId, TrainingSessionAccess access, Guid actorId, CancellationToken ct)
    {
        var name = await _db.TrainingSessions.AsNoTracking()
            .Where(s => s.Id == sessionId).Select(s => new { s.Training.Name, s.SessionDate }).FirstAsync(ct);
        var responders = await _db.TrainingResponses.AsNoTracking()
            .Where(r => r.TrainingSessionId == sessionId && r.UserId != actorId)
            .Select(r => r.UserId).Distinct().ToListAsync(ct);
        if (responders.Count == 0)
        {
            return;
        }

        var payload = new { teamSlug = access.TeamSlug, sessionId, trainingId = access.TrainingId, trainingName = name.Name, kind = "cancelled", sessionDate = name.SessionDate };
        await _notifications.CreateManyAsync(responders, NotificationType.TrainingUpdated, payload, actorId, $"training-updated:{sessionId}:cancelled", ct);
    }

    private static string SeriesLabel(TrainingInterval? interval) => interval switch
    {
        TrainingInterval.Weekly => "weekly",
        TrainingInterval.BiWeekly => "every 2 weeks",
        TrainingInterval.Monthly => "monthly",
        _ => "series",
    };
}
