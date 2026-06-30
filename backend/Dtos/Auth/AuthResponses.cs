namespace JuggerHub.Dtos.Auth;

/// <summary>Generic, neutral message response (register/forgot/resend/verify outcomes).</summary>
public sealed record MessageResponse(string Message);

/// <summary>
/// Returned on a 403 from login when the password was correct but the email is
/// unverified. Revealed only to a caller who knows the password, so not an
/// enumeration oracle.
/// </summary>
public sealed record VerificationRequiredResponse(string Status, string Message)
{
    public static VerificationRequiredResponse Default { get; } =
        new("email_not_verified", "Please verify your email address to sign in.");
}

/// <summary>The authenticated user. Never contains token material.</summary>
public sealed record AuthUserDto(Guid Id, string Email, bool EmailConfirmed);

/// <summary>The published password policy, rendered live by the frontend.</summary>
public sealed record PasswordPolicyDto(
    int MinLength,
    bool RequireDigit,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireNonAlphanumeric,
    int RequiredUniqueChars);
