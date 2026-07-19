namespace JuggerHub.Services.Parties;

/// <summary>
/// Uniform outcome for party operations, mapped to HTTP by the controllers (feature 016).
/// </summary>
public enum PartyOutcome
{
    Ok,
    NotFound,
    Forbidden,
    NotTeamAdmin,
    Invalid,
    Conflict,
    Full,
    Closed,
}

/// <summary>A party operation result carrying a value on success.</summary>
public readonly record struct PartyResult<T>(PartyOutcome Outcome, T? Value, string? Error)
{
    public bool IsOk => Outcome == PartyOutcome.Ok;

    public static PartyResult<T> Ok(T value) => new(PartyOutcome.Ok, value, null);

    public static PartyResult<T> Fail(PartyOutcome outcome, string? error = null) => new(outcome, default, error);
}

/// <summary>A party operation result with no value (void success).</summary>
public readonly record struct PartyResult(PartyOutcome Outcome, string? Error)
{
    public bool IsOk => Outcome == PartyOutcome.Ok;

    public static PartyResult Ok() => new(PartyOutcome.Ok, null);

    public static PartyResult Fail(PartyOutcome outcome, string? error = null) => new(outcome, error);
}
