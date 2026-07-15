namespace JuggerHub.Entities;

/// <summary>
/// A team-scoped training (feature 018): either a recurring <b>series</b> (<see cref="IsRecurring"/> ==
/// true, with a <see cref="Weekday"/>, <see cref="Interval"/> and <see cref="EndDate"/>) or a
/// <b>one-off</b> (<see cref="IsRecurring"/> == false, a single dated session). It is the parent template
/// that owns concrete <see cref="TrainingSession"/> rows; name, description, default location, times and
/// default visibility live here and each session inherits them unless it overrides
/// (<see cref="TrainingSession"/> override columns) or has been individually detached.
/// </summary>
/// <remarks>
/// Trainings behave like <em>internal recurring events</em> — no fee, no participation cap, everyone on
/// the team welcome — and are deliberately separate from <see cref="Event"/>/<see cref="EventSignup"/>,
/// which carry caps, fees, waitlists and approval. There is no whole-training delete in scope; admins
/// skip or cancel individual sessions.
/// </remarks>
public sealed class Training : BaseEntity
{
    public Guid TeamId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public LocationKind LocationKind { get; set; }

    /// <summary>In-person free-text location (e.g. "Sportpark Müngersdorf, Köln"); set when InPerson.</summary>
    public string? Location { get; set; }

    /// <summary>Join link for a virtual training; set when Virtual.</summary>
    public string? VirtualLink { get; set; }

    /// <summary>true = recurring series (Series badge); false = one-off (One-off badge).</summary>
    public bool IsRecurring { get; set; }

    /// <summary>The weekday sessions fall on; series only, null for a one-off.</summary>
    public DayOfWeek? Weekday { get; set; }

    /// <summary>How the series repeats; series only, null for a one-off.</summary>
    public TrainingInterval? Interval { get; set; }

    /// <summary>Default start time-of-day for each session.</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>Default end time-of-day for each session; MUST be after <see cref="StartTime"/>.</summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>First occurrence date (a one-off's single date).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Inclusive last date the series may generate; series only, null for a one-off.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Series-level default visibility; a session may override via its own column.</summary>
    public TrainingVisibility Visibility { get; set; } = TrainingVisibility.TeamOnly;

    /// <summary>The team admin who created the training.</summary>
    public Guid CreatedByUserId { get; set; }

    public Team Team { get; set; } = null!;

    public User CreatedBy { get; set; } = null!;

    public ICollection<TrainingSession> Sessions { get; set; } = [];
}
