using JuggerHub.Common;
using JuggerHub.Services;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the events transactional emails — the co-admin invite (reusing the shared invitation
/// template) and the cancellation notice — and hands the HTML to <see cref="IEmailSender"/>
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

    public async Task SendCoAdminInviteEmailAsync(
        string toEmail,
        string recipientName,
        string eventName,
        string inviterName,
        string token,
        DateTime expiresDate,
        CancellationToken ct = default)
    {
        var url = BuildInviteLink(_options.FrontendBaseUrl, token);
        var html = await _templates.GenerateInvitationEmailAsync(
            recipientName: recipientName,
            inviterName: inviterName,
            inviterEmail: string.Empty,
            organizationName: eventName,
            invitationUrl: url,
            role: "co-admin",
            expirationDate: expiresDate);

        await _sender.SendAsync(toEmail, $"You're invited to co-administer {eventName} — JuggerHub", html, ct);
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
