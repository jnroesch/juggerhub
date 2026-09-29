using JuggerHub.Common;
using JuggerHub.Services;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the events transactional emails — the co-admin invite and the cancellation notice,
/// both in the recipient's language — and hands the HTML to <see cref="IEmailSender"/>
/// (Mailpit locally, Resend on Dev/Prod). Links are built from
/// <see cref="EmailOptions.FrontendBaseUrl"/>. No new infrastructure.
/// </summary>
public sealed class EventEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly IEmailLocalizer _localizer;

    public EventEmailService(
        IEmailTemplateService templates,
        IEmailSender sender,
        IOptions<EmailOptions> options,
        IEmailLocalizer localizer)
    {
        _templates = templates;
        _sender = sender;
        _options = options.Value;
        _localizer = localizer;
    }

    /// <summary>A targeted co-admin invite, in the recipient's language (GH #379).</summary>
    public async Task SendCoAdminInviteEmailAsync(
        string toEmail, string eventName, string inviterName, string token, DateTime expiresDate,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildInviteLink(_options.FrontendBaseUrl, token);
        var html = await _templates.GenerateEventAdminInviteEmailAsync(inviterName, eventName, url, expiresDate, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.eventAdminInvite", culture, eventName), html, ct);
    }

    /// <summary>
    /// The cancellation notice. Rendered from the shared template in the recipient's language
    /// (feature 039) — values are escaped by the template layer, so nothing is encoded here.
    /// </summary>
    public async Task SendCancellationEmailAsync(
        string toEmail, string eventName, Guid eventId,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var eventUrl = BuildEventLink(_options.FrontendBaseUrl, eventId);
        var html = await _templates.GenerateEventCancelledEmailAsync(eventName, eventUrl, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.eventCancelled", culture, eventName), html, ct);
    }

    internal static string BuildInviteLink(string frontendBaseUrl, string token)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/event-invite/{Uri.EscapeDataString(token)}";
    }

    internal static string BuildEventLink(string frontendBaseUrl, Guid eventId)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/events/{eventId}";
    }
}
