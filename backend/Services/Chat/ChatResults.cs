namespace JuggerHub.Services.Chat;

/// <summary>
/// Uniform outcome for chat operations, mapped to HTTP by the controllers (feature 019).
/// </summary>
/// <remarks>
/// Note what is <em>not</em> here: there is no "NotAMember" outcome distinct from
/// <see cref="NotFound"/>. A caller who is not a member of a conversation must not be able to tell
/// whether it exists, so every membership failure collapses into <see cref="NotFound"/> → 404. A 403
/// would confirm existence (spec FR-048).
/// </remarks>
public enum ChatOutcome
{
    Ok,

    /// <summary>Absent — or present but invisible to this caller. The two are deliberately indistinguishable.</summary>
    NotFound,

    /// <summary>The caller may see it but not do this: deleting someone else's message, or a blocked direct send.</summary>
    Forbidden,

    /// <summary>Bad input: empty/over-length body, unnamed group, leaving a team chat, over the group cap.</summary>
    Invalid,

    /// <summary>The conversation is archived and no longer accepts writes.</summary>
    Conflict,
}

/// <summary>A chat operation result carrying a value on success.</summary>
public readonly record struct ChatResult<T>(ChatOutcome Outcome, T? Value, string? Error)
{
    public bool IsOk => Outcome == ChatOutcome.Ok;

    public static ChatResult<T> Ok(T value) => new(ChatOutcome.Ok, value, null);

    public static ChatResult<T> Fail(ChatOutcome outcome, string? error = null) => new(outcome, default, error);
}

/// <summary>A chat operation result with no value (void success).</summary>
public readonly record struct ChatResult(ChatOutcome Outcome, string? Error)
{
    public bool IsOk => Outcome == ChatOutcome.Ok;

    public static ChatResult Ok() => new(ChatOutcome.Ok, null);

    public static ChatResult Fail(ChatOutcome outcome, string? error = null) => new(outcome, error);
}
