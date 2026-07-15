using JuggerHub.Entities;

namespace JuggerHub.Dtos.Trainings;

// ---- Read models -----------------------------------------------------------

/// <summary>A single session as a row in the Trainings tab / public list (feature 018).</summary>
public sealed record TrainingSessionRowDto(
    Guid SessionId,
    Guid TrainingId,
    string Name,
    bool IsOneOff,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    LocationKind LocationKind,
    string? Location,
    string? VirtualLink,
    TrainingVisibility Visibility,
    TrainingSessionStatus Status,
    int GoingCount,
    int MaybeCount,
    int CantCount,
    TrainingRsvp? MyAnswer,
    bool Detached);

/// <summary>A session row plus its team, for the cross-team dashboard agenda.</summary>
public sealed record AgendaSessionDto(
    Guid SessionId,
    Guid TrainingId,
    string Name,
    bool IsOneOff,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    LocationKind LocationKind,
    string? Location,
    string? VirtualLink,
    TrainingVisibility Visibility,
    TrainingSessionStatus Status,
    int GoingCount,
    TrainingRsvp? MyAnswer,
    string TeamSlug,
    string TeamName,
    bool IsPublicGuest);

/// <summary>The admin active-series overview on the Trainings tab.</summary>
public sealed record TrainingSeriesSummaryDto(
    Guid TrainingId,
    string Name,
    DayOfWeek? Weekday,
    TrainingInterval? Interval,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly? EndDate,
    TrainingVisibility Visibility,
    int UpcomingCount,
    DateOnly? NextSessionDate);

/// <summary>The full session page.</summary>
public sealed record TrainingSessionDetailDto(
    Guid SessionId,
    Guid TrainingId,
    string TeamSlug,
    string TeamName,
    string Name,
    string? Description,
    bool IsOneOff,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    LocationKind LocationKind,
    string? Location,
    string? VirtualLink,
    string? SeriesLabel,
    TrainingVisibility Visibility,
    TrainingSessionStatus Status,
    bool IsPast,
    bool IsDetached,
    bool ViewerIsAdmin,
    bool ViewerIsGuest,
    TrainingRsvp? MyAnswer,
    WhosComingDto WhosComing);

/// <summary>Who's coming, grouped by answer (top-N people per group; full list via attendance).</summary>
public sealed record WhosComingDto(
    WhosComingGroupDto Going,
    WhosComingGroupDto Maybe,
    WhosComingGroupDto Cant);

public sealed record WhosComingGroupDto(int Count, IReadOnlyList<WhosComingPersonDto> People);

public sealed record WhosComingPersonDto(
    string Handle,
    string DisplayName,
    string? Position,
    bool IsGuest,
    bool IsYou);

/// <summary>A full attendance row (admin), incl. guests.</summary>
public sealed record AttendanceEntryDto(
    string Handle,
    string DisplayName,
    string? Position,
    bool IsGuest,
    bool IsYou,
    bool IsTeamAdmin,
    TrainingRsvp Answer);

/// <summary>Result of a create.</summary>
public sealed record CreatedTrainingDto(Guid TrainingId, int SessionCount, Guid FirstSessionId);

/// <summary>Result of a whole-series edit.</summary>
public sealed record SeriesEditResultDto(Guid TrainingId, int AddedSessions, int RemovedSessions, int KeptSessions);

// ---- Requests --------------------------------------------------------------

/// <summary>Create a training (series or one-off).</summary>
public sealed record CreateTrainingRequest(
    bool IsRecurring,
    string Name,
    string? Description,
    LocationKind LocationKind,
    string? Location,
    string? VirtualLink,
    DayOfWeek? Weekday,
    TrainingInterval? Interval,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly StartDate,
    DateOnly? EndDate,
    TrainingVisibility Visibility);

/// <summary>Edit the whole series. In-place for time/place/visibility; pattern/end-date changes regenerate.</summary>
public sealed record EditSeriesRequest(
    string? Name,
    string? Description,
    LocationKind? LocationKind,
    string? Location,
    string? VirtualLink,
    DayOfWeek? Weekday,
    TrainingInterval? Interval,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    DateOnly? EndDate,
    TrainingVisibility? Visibility);

/// <summary>Edit a single session — detaches it from the series.</summary>
public sealed record EditSessionRequest(
    DateOnly? SessionDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    LocationKind? LocationKind,
    string? Location,
    string? VirtualLink);

public sealed record SetResponseRequest(TrainingRsvp Answer);

public sealed record SetVisibilityRequest(TrainingVisibility Visibility);
