using JuggerHub.Common;
using JuggerHub.Entities;
using JuggerHub.Services;
using JuggerHub.Services.Localization;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the auth transactional emails: renders the localized HTML templates via
/// <see cref="IEmailTemplateService"/>, builds the SPA link from
/// <see cref="EmailOptions.FrontendBaseUrl"/>, and hands the HTML to
/// <see cref="IEmailSender"/>. The verification/reset tokens are URL-encoded into the
/// link; the SPA reads them from the query and POSTs them back to the API.
///
/// Language (feature 031): verification/reset are addressed to the caller themselves, so they use
/// the <b>request</b> culture (the frontend stamped the effective language on <c>Accept-Language</c>);
/// welcome/password-changed use the <b>recipient's</b> stored preference. Subjects are localized via
/// <see cref="IEmailLocalizer"/>.
/// </summary>
public sealed class AuthEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly IRecipientCultureResolver _culture;
    private readonly IEmailLocalizer _localizer;

    public AuthEmailService(
        IEmailTemplateService templates,
        IEmailSender sender,
        IOptions<EmailOptions> options,
        IRecipientCultureResolver culture,
        IEmailLocalizer localizer)
    {
        _templates = templates;
        _sender = sender;
        _options = options.Value;
        _culture = culture;
        _localizer = localizer;
    }

    public async Task SendVerificationEmailAsync(User user, string token, CancellationToken ct = default)
    {
        // Pre-account: the caller's effective language rode in on Accept-Language (FR-012a).
        var culture = _culture.Resolve(user);
        var url = BuildLink("verify-email", user.Id, token);
        var html = await _templates.GenerateEmailVerificationEmailAsync(DisplayName(user), user.Email!, url, culture);
        await _sender.SendAsync(user.Email!, _localizer.Get("subject.verification", culture), html, ct);
    }

    public async Task SendPasswordResetEmailAsync(User user, string token, CancellationToken ct = default)
    {
        var culture = _culture.Resolve(user);
        var url = BuildLink("reset-password", user.Id, token);
        var html = await _templates.GeneratePasswordResetEmailAsync(url, token, user.Email!, culture);
        await _sender.SendAsync(user.Email!, _localizer.Get("subject.passwordReset", culture), html, ct);
    }

    public async Task SendPasswordChangedNotificationAsync(User user, string ipAddress, CancellationToken ct = default)
    {
        var culture = _culture.Resolve(user);
        var html = await _templates.GeneratePasswordChangeNotificationEmailAsync(
            DisplayName(user), user.Email!, DateTime.UtcNow, ipAddress, culture);
        await _sender.SendAsync(user.Email!, _localizer.Get("subject.passwordChanged", culture), html, ct);
    }

    public async Task SendWelcomeEmailAsync(User user, CancellationToken ct = default)
    {
        var culture = _culture.Resolve(user);
        var html = await _templates.GenerateWelcomeEmailAsync(DisplayName(user), user.Email!, "JuggerHub", DateTime.UtcNow, culture);
        await _sender.SendAsync(user.Email!, _localizer.Get("subject.welcome", culture), html, ct);
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
