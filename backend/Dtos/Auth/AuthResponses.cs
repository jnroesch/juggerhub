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

/// <summary>
/// Returned on a 403 from login when the password was correct but the account is
/// suspended (feature 013). Like the verification response, revealed only to a caller
/// who knows the password — not an enumeration oracle. Banned accounts never get here
/// (they receive the generic invalid-credentials response).
/// </summary>
public sealed record AccountSuspendedResponse(string Status, string Message)
{
    public static AccountSuspendedResponse Default { get; } =
        new("account_suspended", "This account is suspended. Contact support if you think this is a mistake.");
}

/// <summary>
/// The authenticated user. Never contains token material. <see cref="OnboardingCompleted"/>
/// is server-derived (<c>PlayerProfile.OnboardingCompletedAt != null</c>) and is a UX
/// routing hint only — never the authority for gating the onboarding flow (SC-008).
/// </summary>
public sealed record AuthUserDto(Guid Id, string Email, bool EmailConfirmed, bool OnboardingCompleted);

/// <summary>The published password policy, rendered live by the frontend.</summary>
public sealed record PasswordPolicyDto(
    int MinLength,
    bool RequireDigit,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireNonAlphanumeric,
    int RequiredUniqueChars);
