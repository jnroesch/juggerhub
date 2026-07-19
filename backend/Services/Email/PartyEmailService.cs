using JuggerHub.Common;
using JuggerHub.Services;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the party transactional emails (feature 016) — the participation request / nudge, a
/// party news notice, and the co-admin invite (reusing the shared invitation template) — and hands
/// the HTML to <see cref="IEmailSender"/> (Mailpit locally, Resend on Dev/Prod). Links are built
/// from <see cref="EmailOptions.FrontendBaseUrl"/>. No new infrastructure; mirrors
/// <see cref="EventEmailService"/>. Bodies for request/news are finalized with dedicated templates
/// in their stories (party-request.html, party-news.html).
/// </summary>
public sealed class PartyEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;

    public PartyEmailService(IEmailTemplateService templates, IEmailSender sender, IOptions<EmailOptions> options)
    {
        _templates = templates;
        _sender = sender;
        _options = options.Value;
    }

    /// <summary>The participation request (and Nudge): invites a team member to a party.</summary>
    public Task SendPartyRequestEmailAsync(
        string toEmail, string recipientName, string teamName, string eventName,
        string teamSlug, Guid eventId, CancellationToken ct = default)
    {
        var url = BuildPartyLink(_options.FrontendBaseUrl, teamSlug, eventId);
        var safeName = System.Net.WebUtility.HtmlEncode(recipientName);
        var safeTeam = System.Net.WebUtility.HtmlEncode(teamName);
        var safeEvent = System.Net.WebUtility.HtmlEncode(eventName);
        var html =
            $"<p>Hi {safeName},</p>" +
            $"<p><strong>{safeTeam}</strong> is putting a party together for <strong>{safeEvent}</strong>. " +
            "Let the crew know if you're in.</p>" +
            $"<p><a href=\"{url}\">See the request</a></p>" +
            "<p>— JuggerHub</p>";

        return _sender.SendAsync(toEmail, $"Fancy {eventName}? {teamName} is putting a party together — JuggerHub", html, ct);
    }

    /// <summary>A new party news post, sent to the crew.</summary>
    public Task SendPartyNewsEmailAsync(
        string toEmail, string recipientName, string teamName, string eventName,
        string teamSlug, Guid eventId, string body, CancellationToken ct = default)
    {
        var url = BuildPartyLink(_options.FrontendBaseUrl, teamSlug, eventId);
        var safeName = System.Net.WebUtility.HtmlEncode(recipientName);
        var safeTeam = System.Net.WebUtility.HtmlEncode(teamName);
        var safeEvent = System.Net.WebUtility.HtmlEncode(eventName);
        var safeBody = System.Net.WebUtility.HtmlEncode(body);
        var html =
            $"<p>Hi {safeName},</p>" +
            $"<p>New update for the <strong>{safeTeam}</strong> party at <strong>{safeEvent}</strong>:</p>" +
            $"<blockquote>{safeBody}</blockquote>" +
            $"<p><a href=\"{url}\">Open the party</a></p>" +
            "<p>— JuggerHub</p>";

        return _sender.SendAsync(toEmail, $"{teamName} @ {eventName} — party update — JuggerHub", html, ct);
    }

    /// <summary>A targeted co-admin invite, reusing the shared invitation template.</summary>
    public async Task SendCoAdminInviteEmailAsync(
        string toEmail, string recipientName, string teamName, string eventName,
        string inviterName, string token, DateTime expiresDate, CancellationToken ct = default)
    {
        var url = BuildInviteLink(_options.FrontendBaseUrl, token);
        var html = await _templates.GenerateInvitationEmailAsync(
            recipientName: recipientName,
            inviterName: inviterName,
            inviterEmail: string.Empty,
            organizationName: $"{teamName} @ {eventName}",
            invitationUrl: url,
            role: "party co-admin",
            expirationDate: expiresDate);

        await _sender.SendAsync(toEmail, $"You're invited to co-run {teamName}'s party at {eventName} — JuggerHub", html, ct);
    }

    internal static string BuildInviteLink(string frontendBaseUrl, string token)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/party-invite/{Uri.EscapeDataString(token)}";
    }

    internal static string BuildPartyLink(string frontendBaseUrl, string teamSlug, Guid eventId)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/t/{Uri.EscapeDataString(teamSlug)}/party/{eventId}";
    }
}
