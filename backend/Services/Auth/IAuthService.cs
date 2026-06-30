using JuggerHub.Dtos.Auth;

namespace JuggerHub.Services.Auth;

/// <summary>
/// Orchestrates the authentication flows over ASP.NET Core Identity. Returns plain
/// results/DTOs; the thin controller maps them to HTTP responses and sets/clears the
/// auth cookies (the service never touches <c>HttpContext</c>). Enumeration-neutral
/// flows (register/forgot/resend) never reveal whether an account exists.
/// </summary>
public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Returns true if the email was confirmed; false for an expired/used/tampered token.</summary>
    Task<bool> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default);

    Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default);

    Task<LoginResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);

    Task<RefreshResult> RefreshAsync(string? rawRefreshToken, string? ip, CancellationToken ct = default);

    Task LogoutAsync(string? rawRefreshToken, CancellationToken ct = default);

    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    Task<ResetResult> ResetPasswordAsync(ResetPasswordRequest request, string? ip, CancellationToken ct = default);

    PasswordPolicyDto GetPasswordPolicy();

    Task<AuthUserDto?> GetUserAsync(Guid userId, CancellationToken ct = default);
}
