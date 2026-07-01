using JuggerHub.Dtos.Auth;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Profile;
using JuggerHub.Services.Security;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Auth;

/// <summary>
/// Authentication flows over ASP.NET Core Identity. Enumeration-neutral where
/// required (register/forgot/resend), generic failures elsewhere, and the
/// verify-before-login gate is enforced AFTER a correct password so "unverified" is
/// never an enumeration oracle (see research.md §1).
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly AuthEmailService _authEmail;
    private readonly IProfileService _profiles;
    private readonly IdentityOptions _identityOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IJwtTokenService jwt,
        IRefreshTokenService refreshTokens,
        AuthEmailService authEmail,
        IProfileService profiles,
        IOptions<IdentityOptions> identityOptions,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _authEmail = authEmail;
        _profiles = profiles;
        _identityOptions = identityOptions.Value;
        _logger = logger;
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();

        // Validate the password independently of the email, so a weak password is
        // reported without leaking whether the email already exists.
        var policyErrors = await ValidatePasswordAsync(new User { UserName = email, Email = email }, request.Password);
        if (policyErrors.Count > 0)
        {
            return RegisterResult.PolicyViolation(policyErrors);
        }

        // Handle is a PUBLIC identifier (it appears in shareable URLs), so — unlike
        // the email — reporting "invalid" or "taken" is expected UX, not an
        // enumeration oracle. Resolve it before touching the account.
        var handleCheck = await _profiles.ResolveHandleForRegistrationAsync(request.Handle, ct);
        if (handleCheck.Status == HandleCheckStatus.Invalid)
        {
            return RegisterResult.HandleInvalid(handleCheck.Reason ?? "That handle isn't available.");
        }

        if (handleCheck.Status == HandleCheckStatus.Taken)
        {
            return RegisterResult.HandleTaken(handleCheck.Reason ?? "That handle isn't available.");
        }

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Neutral: never reveal the address is taken. Help a legitimate owner who
            // hasn't finished verifying by resending; otherwise stay silent.
            if (!existing.EmailConfirmed)
            {
                await SendVerificationSafelyAsync(existing, ct);
            }

            return RegisterResult.Accepted();
        }

        // Create the account AND its profile atomically: the profile is set on the
        // navigation so Identity's CreateAsync persists both in one SaveChanges.
        // DisplayName defaults to the handle so the public page is never blank.
        var user = new User { UserName = email, Email = email };
        user.Profile = new PlayerProfile
        {
            Handle = handleCheck.Normalized,
            DisplayName = handleCheck.Normalized,
        };

        IdentityResult created;
        try
        {
            created = await _userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException)
        {
            // The unique-handle index guards against a race the pre-check missed.
            return RegisterResult.HandleTaken("That handle isn't available.");
        }

        if (!created.Succeeded)
        {
            // Password was pre-validated, so this is a race/duplicate or similar —
            // stay neutral. Log codes only (no values).
            _logger.LogWarning("Registration create did not succeed: {Codes}",
                string.Join(',', created.Errors.Select(e => e.Code)));
            return RegisterResult.Accepted();
        }

        await SendVerificationSafelyAsync(user, ct);
        return RegisterResult.Accepted();
    }

    public async Task<bool> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return false;
        }

        if (user.EmailConfirmed)
        {
            return true; // idempotent
        }

        var result = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            return false;
        }

        try
        {
            await _authEmail.SendWelcomeEmailAsync(user, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email");
        }

        return true;
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.EmailConfirmed)
        {
            await SendVerificationSafelyAsync(user, ct);
        }
        // Always neutral — no signal whether the account exists or is already verified.
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Flatten timing vs the wrong-password path by doing comparable hash work.
            _userManager.PasswordHasher.HashPassword(new User(), request.Password);
            return LoginResult.Failed();
        }

        // Validates the password and drives lockout, but (with RequireConfirmedEmail
        // left false) does NOT short-circuit on unverified email — so we can reveal
        // "unverified" only after a correct password.
        var check = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            // Wrong password OR locked out → one generic failure (no disclosure).
            return LoginResult.Failed();
        }

        if (!user.EmailConfirmed)
        {
            return LoginResult.NeedsVerification();
        }

        // MFA seam: a future feature checks user.TwoFactorEnabled here and returns
        // LoginResult with PendingTwoFactor instead of issuing tokens (research §7).

        var tokens = await IssueTokensAsync(user, request.RememberMe, ip, familyId: null, ct);
        return LoginResult.Success(user.Adapt<AuthUserDto>(), tokens);
    }

    public async Task<RefreshResult> RefreshAsync(string? rawRefreshToken, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return RefreshResult.Rejected();
        }

        var rotate = await _refreshTokens.RotateAsync(rawRefreshToken, ip, ct);
        if (rotate.Status != RotateStatus.Success || rotate.Issued is null)
        {
            return RefreshResult.Rejected();
        }

        var user = await _userManager.FindByIdAsync(rotate.UserId.ToString());
        if (user is null)
        {
            return RefreshResult.Rejected();
        }

        var (accessToken, accessExpires) = _jwt.CreateAccessToken(user);
        var issued = rotate.Issued.Value;
        var tokens = new IssuedTokens(
            accessToken, accessExpires,
            issued.RawToken, ToUtcOffset(issued.ExpiresAt), issued.IsPersistent);
        return RefreshResult.Success(user.Adapt<AuthUserDto>(), tokens);
    }

    public Task LogoutAsync(string? rawRefreshToken, CancellationToken ct = default)
    {
        return string.IsNullOrEmpty(rawRefreshToken)
            ? Task.CompletedTask
            : _refreshTokens.RevokeAsync(rawRefreshToken, "logout", ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                await _authEmail.SendPasswordResetEmailAsync(user, token, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email");
            }
        }
        // Always neutral — identical response whether or not the address exists.
    }

    public async Task<ResetResult> ResetPasswordAsync(ResetPasswordRequest request, string? ip, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return ResetResult.InvalidToken();
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (result.Succeeded)
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, "password-reset", ct);
            try
            {
                await _authEmail.SendPasswordChangedNotificationAsync(user, ip ?? "unknown", ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password change notification");
            }

            return ResetResult.Success();
        }

        if (result.Errors.Any(e => e.Code.Contains("Token", StringComparison.OrdinalIgnoreCase)))
        {
            return ResetResult.InvalidToken();
        }

        var passwordErrors = result.Errors
            .Where(e => e.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Description)
            .ToList();
        return passwordErrors.Count > 0
            ? ResetResult.PolicyViolation(passwordErrors)
            : ResetResult.InvalidToken();
    }

    public PasswordPolicyDto GetPasswordPolicy()
    {
        var p = _identityOptions.Password;
        return new PasswordPolicyDto(
            p.RequiredLength,
            p.RequireDigit,
            p.RequireLowercase,
            p.RequireUppercase,
            p.RequireNonAlphanumeric,
            p.RequiredUniqueChars);
    }

    public async Task<AuthUserDto?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.Adapt<AuthUserDto>();
    }

    private async Task<IssuedTokens> IssueTokensAsync(User user, bool rememberMe, string? ip, Guid? familyId, CancellationToken ct)
    {
        var (accessToken, accessExpires) = _jwt.CreateAccessToken(user);
        var refresh = await _refreshTokens.IssueAsync(user.Id, rememberMe, ip, familyId, ct);
        return new IssuedTokens(accessToken, accessExpires, refresh.RawToken, ToUtcOffset(refresh.ExpiresAt), refresh.IsPersistent);
    }

    private async Task<IReadOnlyList<string>> ValidatePasswordAsync(User user, string password)
    {
        var errors = new List<string>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, password);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors.Select(e => e.Description));
            }
        }

        return errors;
    }

    private async Task SendVerificationSafelyAsync(User user, CancellationToken ct)
    {
        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _authEmail.SendVerificationEmailAsync(user, token, ct);
        }
        catch (Exception ex)
        {
            // Email failures must never change the neutral client response or leak.
            _logger.LogError(ex, "Failed to send verification email");
        }
    }

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
