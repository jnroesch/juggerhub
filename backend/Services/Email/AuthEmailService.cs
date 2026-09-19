using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services;
using JuggerHub.Services.Localization;
using JuggerHub.Services.Teams;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the auth transactional emails: renders the localized HTML templates via
/// <see cref="IEmailTemplateService"/>, builds the SPA link from
/// <see cref="EmailOptions.FrontendBaseUrl"/>, and hands the HTML to
/// <see cref="IEmailSender"/>. The verification/reset tokens are URL-encoded into the
/// link; the SPA reads them from the query and POSTs them back to the API.
///
/// Feature 053: a verification link may also carry an <see cref="InviteReference"/> — the two
/// segments of the shared invite link the person registered from — as <c>inviteSlug</c> and
/// <c>inviteToken</c>, each URL-encoded on its own. The reference rides <b>next to</b> the
/// verification token, never inside it, and the link never carries a path: the SPA composes the
/// invite page from the two validated parts. The template prints whatever URL it is handed, so
/// nothing about the email body changes.
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
    private readonly AppDbContext _db;

    public AuthEmailService(
        IEmailTemplateService templates,
        IEmailSender sender,
        IOptions<EmailOptions> options,
        IRecipientCultureResolver culture,
        IEmailLocalizer localizer,
        AppDbContext db)
    {
        _templates = templates;
        _sender = sender;
        _options = options.Value;
        _culture = culture;
        _localizer = localizer;
        _db = db;
    }

    public async Task SendVerificationEmailAsync(User user, string token, InviteReference? invite, CancellationToken ct = default)
    {
        // Pre-account: the caller's effective language rode in on Accept-Language (FR-012a).
        var culture = _culture.Resolve(user);
        var url = BuildLink("verify-email", user.Id, token, invite);
        var name = await EmailRecipientName.ForAsync(_db, user, ct);
        var html = await _templates.GenerateEmailVerificationEmailAsync(name, user.Email!, url, culture);
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
        var name = await EmailRecipientName.ForAsync(_db, user, ct);
        var html = await _templates.GeneratePasswordChangeNotificationEmailAsync(
            name, user.Email!, DateTime.UtcNow, ipAddress, culture);
        await _sender.SendAsync(user.Email!, _localizer.Get("subject.passwordChanged", culture), html, ct);
    }

    public async Task SendWelcomeEmailAsync(User user, CancellationToken ct = default)
    {
        var culture = _culture.Resolve(user);
        var name = await EmailRecipientName.ForAsync(_db, user, ct);
        var html = await _templates.GenerateWelcomeEmailAsync(name, user.Email!, "JuggerHub", DateTime.UtcNow, culture);
        await _sender.SendAsync(user.Email!, _localizer.Get("subject.welcome", culture), html, ct);
    }

    private string BuildLink(string path, Guid userId, string token, InviteReference? invite = null)
    {
        var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(token);
        var link = $"{baseUrl}/{path}?userId={userId}&token={encodedToken}";
        if (invite is { } inviteRef)
        {
            link += $"&inviteSlug={Uri.EscapeDataString(inviteRef.Slug)}&inviteToken={Uri.EscapeDataString(inviteRef.Token)}";
        }

        return link;
    }
}
