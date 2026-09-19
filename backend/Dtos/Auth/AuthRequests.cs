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
/// <param name="InviteSlug">
/// Feature 053. With <paramref name="InviteToken"/>, an opaque reference to the shared invite link
/// the person came from (its two path segments). Validated for shape only, never stored, never
/// read against the invitation; its one effect is that the verification link carries it, so the
/// invitation survives the email hop. A malformed pair is dropped silently — registration
/// proceeds exactly as if nothing had been sent.
/// </param>
/// <param name="InviteToken">See <paramref name="InviteSlug"/>.</param>
public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required] string Password,
    [Required, MaxLength(30)] string Handle,
    [Required] bool AcceptsTerms,
    [Required, MaxLength(32)] string TermsVersion,
    [Required, MaxLength(8)] string TermsLanguage,
    [MaxLength(64)] string? InviteSlug = null,
    [MaxLength(128)] string? InviteToken = null);

/// <summary>Sign in. <see cref="RememberMe"/> drives persistent vs session cookies.</summary>
public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false);

/// <summary>Request a password-reset link (enumeration-neutral).</summary>
public sealed record ForgotPasswordRequest(
    [Required, EmailAddress] string Email);

/// <summary>
/// Resend the verification email (enumeration-neutral). The optional invite pair is the same
/// shape-only reference <see cref="RegisterRequest"/> carries (feature 053): a screen that still
/// knows which invite the person came from passes it on so the re-sent link carries it too.
/// </summary>
public sealed record ResendVerificationRequest(
    [Required, EmailAddress] string Email,
    [MaxLength(64)] string? InviteSlug = null,
    [MaxLength(128)] string? InviteToken = null);

/// <summary>Confirm email ownership via the emailed token.</summary>
public sealed record VerifyEmailRequest(
    [Required] Guid UserId,
    [Required] string Token);

/// <summary>Set a new password via the emailed reset token.</summary>
public sealed record ResetPasswordRequest(
    [Required] Guid UserId,
    [Required] string Token,
    [Required] string NewPassword);
