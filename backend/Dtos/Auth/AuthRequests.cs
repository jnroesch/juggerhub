using System.ComponentModel.DataAnnotations;

namespace JuggerHub.Dtos.Auth;

// Note: validation attributes go on the record's *constructor parameters* (no
// `property:` target). MVC reads parameter-level metadata for positional records
// and throws if it's on the generated property instead.

/// <summary>
/// Register a new account with email + password + an immutable handle (feature 003), plus an
/// active agreement to the Terms of Use (feature 041).
/// </summary>
/// <param name="Email">The account's email address; also becomes the Identity username.</param>
/// <param name="Password">Validated against the backend password policy before anything else.</param>
/// <param name="Handle">The immutable public identifier, normalized and uniqueness-checked.</param>
/// <param name="AcceptsTerms">
/// The affirmative act, mapping 1:1 to the registration form's checkbox. Kept distinct from
/// <paramref name="TermsVersion"/> so "never agreed" and "agreed against stale text" are two
/// different refusals with two different fixes.
/// </param>
/// <param name="TermsVersion">
/// The version the client actually displayed. Validated against the server's current version and
/// then <b>discarded</b> — the row records the server's own value. Requiring it is what proves the
/// client rendered the current document rather than a stale cached one (spec FR-020, research R1).
/// </param>
/// <param name="TermsLanguage">
/// The translation the document was shown in. Validated against the supported allowlist; without
/// that check a client could write arbitrary text into an evidence row.
/// </param>
public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required] string Password,
    [Required, MaxLength(30)] string Handle,
    [Required] bool AcceptsTerms,
    [Required, MaxLength(32)] string TermsVersion,
    [Required, MaxLength(8)] string TermsLanguage);

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
