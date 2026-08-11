using JuggerHub.Entities;

namespace JuggerHub.Dtos.Trainings;

// ---- Read models -----------------------------------------------------------

/// <summary>
/// A single session as a row in the Trainings tab / public list (feature 018).
/// <paramref name="LocationLabel"/> is built server-side (feature 042) so a training and an event
/// at the same address read identically; a list never renders street or postal code, so they are
/// deliberately absent here.
/// </summary>
public sealed record TrainingSessionRowDto(
    Guid SessionId,
    Guid TrainingId,
    string Name,
    bool IsOneOff,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    LocationKind LocationKind,
    string LocationLabel,
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
    string LocationLabel,
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
    DateOnly? NextSessionDate,
    Guid? NextSessionId);

/// <summary>
/// The full session page. Carries the whole structured address (feature 042) because the edit
/// forms prefill from it — including <paramref name="Location"/>, the resolved city, which
/// <c>jh-city-picker</c> reads back as its current selection.
/// </summary>
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
    string? VenueName,
    string? Street,
    string? PostalCode,
    JuggerHub.Dtos.Cities.LocationDto? Location,
    string LocationLabel,
    string? VirtualLink,
    string? SeriesLabel,
    DayOfWeek? Weekday,
    TrainingInterval? Interval,
    DateOnly? EndDate,
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
    bool HasAvatar,
    string? Position,
    bool IsGuest,
    bool IsYou);

/// <summary>A full attendance row (admin), incl. guests. <see cref="UserId"/> drives guest removal.</summary>
public sealed record AttendanceEntryDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    bool HasAvatar,
    string? Position,
    bool IsGuest,
    bool IsYou,
    bool IsTeamAdmin,
    TrainingRsvp Answer);

/// <summary>Result of a create.</summary>
public sealed record CreatedTrainingDto(Guid TrainingId, int SessionCount, Guid FirstSessionId);

/// <summary>Result of a whole-series edit.</summary>
/// <remarks>
/// <paramref name="NextSessionId"/> is the surviving entry point: the edit form is session-keyed and a
/// pattern change hard-deletes the session it was opened from, so the caller cannot navigate back to
/// where it came from (GH #181). Same filter and order as <see cref="TrainingSeriesSummaryDto"/>'s
/// <c>NextSessionId</c>, so the two never disagree. Null only if nothing upcoming survives.
/// </remarks>
public sealed record SeriesEditResultDto(
    Guid TrainingId, int AddedSessions, int RemovedSessions, int KeptSessions, Guid? NextSessionId);

// ---- Requests --------------------------------------------------------------

/// <summary>
/// Create a training (series or one-off). For an in-person training <see cref="Street"/>,
/// <see cref="PostalCode"/> and <see cref="Location"/> are all required; a venue name is optional.
/// Mirrors <c>CreateEventRequest</c> (feature 042).
/// </summary>
public sealed record CreateTrainingRequest(
    bool IsRecurring,
    string Name,
    string? Description,
    LocationKind LocationKind,
    string? VenueName,
    string? Street,
    string? PostalCode,
    JuggerHub.Dtos.Cities.LocationSelectionDto? Location,
    string? VirtualLink,
    DayOfWeek? Weekday,
    TrainingInterval? Interval,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly StartDate,
    DateOnly? EndDate,
    TrainingVisibility Visibility);

/// <summary>
/// Edit the whole series. In-place for time/place/visibility; pattern/end-date changes regenerate.
/// </summary>
/// <remarks>
/// The address is replaced as a BLOCK: when <see cref="Location"/> is present, venue/street/postal/city
/// are applied together and an omitted member is stored as null. It is never patched field by field —
/// that would allow a street from one address against a city from another (042 FR-007).
/// </remarks>
public sealed record EditSeriesRequest(
    string? Name,
    string? Description,
    LocationKind? LocationKind,
    string? VenueName,
    string? Street,
    string? PostalCode,
    JuggerHub.Dtos.Cities.LocationSelectionDto? Location,
    string? VirtualLink,
    DayOfWeek? Weekday,
    TrainingInterval? Interval,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    DateOnly? EndDate,
    TrainingVisibility? Visibility);

/// <summary>Edit a single session — detaches it from the series.</summary>
/// <remarks>
/// Supplying <see cref="Location"/> relocates the session: street, postal code and city are required
/// together and are stored as the session's own indivisible address block (042 FR-006/FR-007).
/// </remarks>
public sealed record EditSessionRequest(
    DateOnly? SessionDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    LocationKind? LocationKind,
    string? VenueName,
    string? Street,
    string? PostalCode,
    JuggerHub.Dtos.Cities.LocationSelectionDto? Location,
    string? VirtualLink);

public sealed record SetResponseRequest(TrainingRsvp Answer);

public sealed record SetVisibilityRequest(TrainingVisibility Visibility);
