using JuggerHub.Common;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the marketplace transactional email (feature 017) — a party's invite to a mercenary — and
/// hands the HTML to <see cref="IEmailSender"/> (Mailpit locally, Resend on Dev/Prod). The link points
/// at the event page, where the invited player's market inbox lives. No new infrastructure; mirrors
/// <see cref="PartyEmailService"/>.
/// </summary>
public sealed class MarketEmailService
{
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;

    public MarketEmailService(IEmailSender sender, IOptions<EmailOptions> options)
    {
        _sender = sender;
        _options = options.Value;
    }

    /// <summary>A party's invite to a free agent: nudges them to answer on the event page.</summary>
    public Task SendMarketInviteEmailAsync(
        string toEmail, string recipientName, string teamName, string eventName,
        string inviterName, Guid eventId, CancellationToken ct = default)
    {
        var url = BuildEventLink(_options.FrontendBaseUrl, eventId);
        var safeName = System.Net.WebUtility.HtmlEncode(recipientName);
        var safeTeam = System.Net.WebUtility.HtmlEncode(teamName);
        var safeEvent = System.Net.WebUtility.HtmlEncode(eventName);
        var safeInviter = System.Net.WebUtility.HtmlEncode(inviterName);
        var html =
            $"<p>Hi {safeName},</p>" +
            $"<p><strong>{safeInviter}</strong> invited you to play for <strong>{safeTeam}</strong>'s crew at " +
            $"<strong>{safeEvent}</strong>. Nothing happens until you accept — take a look and let them know.</p>" +
            $"<p><a href=\"{url}\">See the invite</a></p>" +
            "<p>— JuggerHub</p>";

        return _sender.SendAsync(toEmail, $"{teamName} wants you at {eventName} — JuggerHub", html, ct);
    }

    internal static string BuildEventLink(string frontendBaseUrl, Guid eventId)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/events/{eventId}";
    }
}
