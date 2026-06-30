using System.ComponentModel.DataAnnotations;

namespace JuggerHub.Dtos.Auth;

// Note: validation attributes go on the record's *constructor parameters* (no
// `property:` target). MVC reads parameter-level metadata for positional records
// and throws if it's on the generated property instead.

/// <summary>Register a new account with email + password.</summary>
public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required] string Password);

/// <summary>Sign in. <see cref="RememberMe"/> drives persistent vs session cookies.</summary>
public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false);

/// <summary>Request a password-reset link (enumeration-neutral).</summary>
public sealed record ForgotPasswordRequest(
    [Required, EmailAddress] string Email);

/// <summary>Resend the verification email (enumeration-neutral).</summary>
public sealed record ResendVerificationRequest(
    [Required, EmailAddress] string Email);

/// <summary>Confirm email ownership via the emailed token.</summary>
public sealed record VerifyEmailRequest(
    [Required] Guid UserId,
    [Required] string Token);

/// <summary>Set a new password via the emailed reset token.</summary>
public sealed record ResetPasswordRequest(
    [Required] Guid UserId,
    [Required] string Token,
    [Required] string NewPassword);
