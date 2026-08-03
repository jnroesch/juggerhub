using JuggerHub.Dtos.Auth;

namespace JuggerHub.Services.Auth;

public enum LoginStatus
{
    Succeeded,
    RequiresEmailVerification,
    PendingTwoFactor, // reserved for the future MFA feature — see research.md §7 (never returned today)
    Failed,

    /// <summary>
    /// Correct password, but the account is suspended (feature 013). Revealed only to a
    /// caller who knows the password — not an enumeration oracle. A BANNED account never
    /// reaches this: it returns the generic <see cref="Failed"/> (a banned account is
    /// indistinguishable from a nonexistent one).
    /// </summary>
    Suspended,
}

public enum RegisterStatus
{
    Accepted,
    PasswordPolicyViolation,
    HandleInvalid,
    HandleTaken,

    /// <summary>
    /// No agreement to the Terms of Use was given (feature 041, FR-018). Returned before any
    /// account lookup, so it never interacts with the enumeration-neutral <see cref="Accepted"/>
    /// path — a terms refusal depends only on values the caller sent.
    /// </summary>
    TermsNotAccepted,

    /// <summary>
    /// Agreement was given, but against a version that is no longer current — a stale cached
    /// catalogue or a tab left open across a deploy. Distinct from
    /// <see cref="TermsNotAccepted"/> because the fix is different: reload and read the current
    /// text, rather than tick a box.
    /// </summary>
    TermsVersionMismatch,

    /// <summary>The display language submitted with the acceptance is not a supported one.</summary>
    TermsLanguageUnsupported,
}

public enum ResetStatus
{
    Success,
    InvalidToken,
    PasswordPolicyViolation,
}

public enum RefreshStatus
{
    Succeeded,
    Rejected,
}

/// <summary>Tokens issued by login/refresh, for the controller to set as cookies. No token is ever returned in a response body.</summary>
public readonly record struct IssuedTokens(
    string AccessToken,
    DateTimeOffset AccessExpires,
    string RefreshToken,
    DateTimeOffset RefreshExpires,
    bool IsPersistent);

public sealed record RegisterResult(RegisterStatus Status, IReadOnlyList<string> Errors)
{
    public static RegisterResult Accepted() => new(RegisterStatus.Accepted, []);
    public static RegisterResult PolicyViolation(IReadOnlyList<string> errors) =>
        new(RegisterStatus.PasswordPolicyViolation, errors);

    // Terms rejections are NOT enumeration-sensitive either: they depend only on values the
    // caller sent, never on whether an account exists. That is why they are checked first and
    // reported plainly rather than folded into the neutral Accepted() response, which would tell
    // someone to check their email for an account that was never created (feature 041).
    public static RegisterResult TermsNotAccepted(string reason) =>
        new(RegisterStatus.TermsNotAccepted, [reason]);
    public static RegisterResult TermsVersionMismatch(string reason) =>
        new(RegisterStatus.TermsVersionMismatch, [reason]);
    public static RegisterResult TermsLanguageUnsupported(string reason) =>
        new(RegisterStatus.TermsLanguageUnsupported, [reason]);
    // Handle rejections are NOT enumeration-sensitive: handles are public identifiers
    // by design (they appear in shareable URLs), so reporting them is expected UX.
    public static RegisterResult HandleInvalid(string reason) =>
        new(RegisterStatus.HandleInvalid, [reason]);
    public static RegisterResult HandleTaken(string reason) =>
        new(RegisterStatus.HandleTaken, [reason]);
}

public sealed class LoginResult
{
    public LoginStatus Status { get; init; }
    public AuthUserDto? User { get; init; }
    public IssuedTokens? Tokens { get; init; }

    public static LoginResult Failed() => new() { Status = LoginStatus.Failed };
    public static LoginResult NeedsVerification() => new() { Status = LoginStatus.RequiresEmailVerification };
    public static LoginResult AccountSuspended() => new() { Status = LoginStatus.Suspended };
    public static LoginResult Success(AuthUserDto user, IssuedTokens tokens) =>
        new() { Status = LoginStatus.Succeeded, User = user, Tokens = tokens };
}

public sealed class RefreshResult
{
    public RefreshStatus Status { get; init; }
    public AuthUserDto? User { get; init; }
    public IssuedTokens? Tokens { get; init; }

    public static RefreshResult Rejected() => new() { Status = RefreshStatus.Rejected };
    public static RefreshResult Success(AuthUserDto user, IssuedTokens tokens) =>
        new() { Status = RefreshStatus.Succeeded, User = user, Tokens = tokens };
}

public sealed record ResetResult(ResetStatus Status, IReadOnlyList<string> Errors)
{
    public static ResetResult Success() => new(ResetStatus.Success, []);
    public static ResetResult InvalidToken() => new(ResetStatus.InvalidToken, []);
    public static ResetResult PolicyViolation(IReadOnlyList<string> errors) =>
        new(ResetStatus.PasswordPolicyViolation, errors);
}
