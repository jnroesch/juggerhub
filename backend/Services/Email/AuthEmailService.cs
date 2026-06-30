using JuggerHub.Common;
using JuggerHub.Entities;
using JuggerHub.Services;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the auth transactional emails: renders the existing HTML templates via
/// <see cref="IEmailTemplateService"/>, builds the SPA link from
/// <see cref="EmailOptions.FrontendBaseUrl"/>, and hands the HTML to
/// <see cref="IEmailSender"/>. The verification/reset tokens are URL-encoded into the
/// link; the SPA reads them from the query and POSTs them back to the API.
/// </summary>
public sealed class AuthEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;

    public AuthEmailService(IEmailTemplateService templates, IEmailSender sender, IOptions<EmailOptions> options)
    {
        _templates = templates;
        _sender = sender;
        _options = options.Value;
    }

    public async Task SendVerificationEmailAsync(User user, string token, CancellationToken ct = default)
    {
        var url = BuildLink("verify-email", user.Id, token);
        var html = await _templates.GenerateEmailVerificationEmailAsync(DisplayName(user), user.Email!, url);
        await _sender.SendAsync(user.Email!, "Verify your email — JuggerHub", html, ct);
    }

    public async Task SendPasswordResetEmailAsync(User user, string token, CancellationToken ct = default)
    {
        var url = BuildLink("reset-password", user.Id, token);
        var html = await _templates.GeneratePasswordResetEmailAsync(url, token, user.Email!);
        await _sender.SendAsync(user.Email!, "Reset your password — JuggerHub", html, ct);
    }

    public async Task SendPasswordChangedNotificationAsync(User user, string ipAddress, CancellationToken ct = default)
    {
        var html = await _templates.GeneratePasswordChangeNotificationEmailAsync(
            DisplayName(user), user.Email!, DateTime.UtcNow, ipAddress);
        await _sender.SendAsync(user.Email!, "Your password was changed — JuggerHub", html, ct);
    }

    public async Task SendWelcomeEmailAsync(User user, CancellationToken ct = default)
    {
        var html = await _templates.GenerateWelcomeEmailAsync(DisplayName(user), user.Email!, "JuggerHub", DateTime.UtcNow);
        await _sender.SendAsync(user.Email!, "Welcome to JuggerHub", html, ct);
    }

    private string BuildLink(string path, Guid userId, string token)
    {
        var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(token);
        return $"{baseUrl}/{path}?userId={userId}&token={encodedToken}";
    }

    private static string DisplayName(User user) =>
        user.Email is { Length: > 0 } email ? email.Split('@')[0] : "there";
}
