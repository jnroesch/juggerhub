using JuggerHub.Common;
using JuggerHub.Services;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the marketplace transactional email (feature 017) — a party's invite to a mercenary — and
/// hands the HTML to <see cref="IEmailSender"/> (Mailpit locally, Resend on Dev/Prod). The link points
/// at the event page, where the invited player's market inbox lives. No new infrastructure; mirrors
/// <see cref="PartyEmailService"/>.
///
/// Feature 039 moved the body onto the shared <c>market-invite.html</c> template, so it now carries
/// the standard chrome and renders in the recipient's language.
/// </summary>
public sealed class MarketEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly IEmailLocalizer _localizer;

    public MarketEmailService(
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

    /// <summary>A party's invite to a free agent: nudges them to answer on the event page.</summary>
    public async Task SendMarketInviteEmailAsync(
        string toEmail, string recipientName, string teamName, string eventName,
        string inviterName, Guid eventId, string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildEventLink(_options.FrontendBaseUrl, eventId);
        var html = await _templates.GenerateMarketInviteEmailAsync(recipientName, teamName, eventName, inviterName, url, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.marketInvite", culture, teamName, eventName), html, ct);
    }

    internal static string BuildEventLink(string frontendBaseUrl, Guid eventId)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/events/{eventId}";
    }
}
