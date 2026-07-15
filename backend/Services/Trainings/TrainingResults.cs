namespace JuggerHub.Services.Trainings;

/// <summary>
/// Uniform outcome for training operations, mapped to HTTP by the controllers (feature 018).
/// </summary>
public enum TrainingOutcome
{
    Ok,
    NotFound,
    Forbidden,
    NotTeamAdmin,
    Invalid,
    Conflict,
}

/// <summary>A training operation result carrying a value on success.</summary>
public readonly record struct TrainingResult<T>(TrainingOutcome Outcome, T? Value, string? Error)
{
    public bool IsOk => Outcome == TrainingOutcome.Ok;

    public static TrainingResult<T> Ok(T value) => new(TrainingOutcome.Ok, value, null);

    public static TrainingResult<T> Fail(TrainingOutcome outcome, string? error = null) => new(outcome, default, error);
}

/// <summary>A training operation result with no value (void success).</summary>
public readonly record struct TrainingResult(TrainingOutcome Outcome, string? Error)
{
    public bool IsOk => Outcome == TrainingOutcome.Ok;

    public static TrainingResult Ok() => new(TrainingOutcome.Ok, null);

    public static TrainingResult Fail(TrainingOutcome outcome, string? error = null) => new(outcome, error);
}
