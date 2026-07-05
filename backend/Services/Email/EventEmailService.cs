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

    public EventEmailService(IEmailTemplateService templates, IEmailSender sender, IOptions<EmailOptions> options)
    {
        _templates = templates;
        _sender = sender;
        _options = options.Value;
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

    public Task SendCancellationEmailAsync(
        string toEmail, string recipientName, string eventName, Guid eventId, CancellationToken ct = default)
    {
        var eventUrl = BuildEventLink(_options.FrontendBaseUrl, eventId);
        var safeName = System.Net.WebUtility.HtmlEncode(recipientName);
        var safeEvent = System.Net.WebUtility.HtmlEncode(eventName);
        var html =
            $"<p>Hi {safeName},</p>" +
            $"<p><strong>{safeEvent}</strong> has been cancelled by the organiser. No further sign-ups or " +
            "waiting-list joins are being accepted.</p>" +
            $"<p>You can still view the event page here: <a href=\"{eventUrl}\">{eventUrl}</a></p>" +
            "<p>— JuggerHub</p>";

        return _sender.SendAsync(toEmail, $"{eventName} has been cancelled — JuggerHub", html, ct);
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
