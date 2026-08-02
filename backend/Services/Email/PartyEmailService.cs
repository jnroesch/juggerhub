using JuggerHub.Common;
using JuggerHub.Services;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the party transactional emails (feature 016) — the participation request / nudge, a
/// party news notice, and the co-admin invite (reusing the shared invitation template) — and hands
/// the HTML to <see cref="IEmailSender"/> (Mailpit locally, Resend on Dev/Prod). Links are built
/// from <see cref="EmailOptions.FrontendBaseUrl"/>. No new infrastructure; mirrors
/// <see cref="EventEmailService"/>.
///
/// Feature 039 replaced the hand-rolled request/news bodies with the dedicated
/// <c>party-request.html</c> / <c>party-news.html</c> templates promised here, so all three
/// messages now carry the shared chrome and render in the recipient's language.
/// </summary>
public sealed class PartyEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly IEmailLocalizer _localizer;

    public PartyEmailService(
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

    /// <summary>The participation request (and Nudge): invites a team member to a party.</summary>
    public async Task SendPartyRequestEmailAsync(
        string toEmail, string recipientName, string teamName, string eventName,
        string teamSlug, Guid eventId, string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildPartyLink(_options.FrontendBaseUrl, teamSlug, eventId);
        var html = await _templates.GeneratePartyRequestEmailAsync(recipientName, teamName, eventName, url, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.partyRequest", culture, eventName, teamName), html, ct);
    }

    /// <summary>
    /// A new party news post, sent to the crew. <paramref name="excerpt"/> arrives already
    /// truncated — the caller owns that, exactly as the team-news path does.
    /// </summary>
    public async Task SendPartyNewsEmailAsync(
        string toEmail, string recipientName, string teamName, string eventName,
        string teamSlug, Guid eventId, string excerpt, string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildPartyLink(_options.FrontendBaseUrl, teamSlug, eventId);
        var html = await _templates.GeneratePartyNewsEmailAsync(recipientName, teamName, eventName, excerpt, url, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.partyNews", culture, teamName, eventName), html, ct);
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
