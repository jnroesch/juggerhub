using JuggerHub.Dtos.Auth;

namespace JuggerHub.Services.Auth;

public enum LoginStatus
{
    Succeeded,
    RequiresEmailVerification,
    PendingTwoFactor, // reserved for the future MFA feature — see research.md §7 (never returned today)
    Failed,
}

public enum RegisterStatus
{
    Accepted,
    PasswordPolicyViolation,
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
}

public sealed class LoginResult
{
    public LoginStatus Status { get; init; }
    public AuthUserDto? User { get; init; }
    public IssuedTokens? Tokens { get; init; }

    public static LoginResult Failed() => new() { Status = LoginStatus.Failed };
    public static LoginResult NeedsVerification() => new() { Status = LoginStatus.RequiresEmailVerification };
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
