namespace JuggerHub.Entities;

/// <summary>
/// How often a recurring training <see cref="Training"/> repeats on its chosen weekday (feature 018).
/// <see cref="Monthly"/> is the same weekday-of-month position as the first occurrence (e.g. "3rd
/// Tuesday"), not a fixed 28-day cadence. Series only — a one-off carries no interval. Serialized as its
/// name (global JsonStringEnumConverter).
/// </summary>
public enum TrainingInterval
{
    Weekly = 0,
    BiWeekly = 1,
    Monthly = 2,
}

/// <summary>
/// Who may see and RSVP a training or a single session (feature 018). <see cref="TeamOnly"/> is
/// members-only; <see cref="Public"/> opens it to any signed-in non-member (an "open mat") who then
/// shows as a guest. Set on the series and overridable per session. Serialized as its name.
/// </summary>
public enum TrainingVisibility
{
    TeamOnly = 0,
    Public = 1,
}

/// <summary>
/// Lifecycle of a single <see cref="TrainingSession"/> (feature 018). <see cref="Cancelled"/> stays
/// visible marked-off and notifies responders; <see cref="Skipped"/> is dropped quietly (hidden, no
/// notification). Both block new responses and are terminal. Serialized as its name.
/// </summary>
public enum TrainingSessionStatus
{
    Scheduled = 0,
    Cancelled = 1,
    Skipped = 2,
}

/// <summary>
/// A member's or guest's three-way answer to a session (feature 018). No cap — everyone welcome.
/// Exactly one current answer per person per session. Serialized as its name.
/// </summary>
public enum TrainingRsvp
{
    Going = 0,
    Maybe = 1,
    Cant = 2,
}
