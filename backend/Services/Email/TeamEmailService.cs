using JuggerHub.Common;
using JuggerHub.Services;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the team-invite transactional email: renders the existing shared invitation
/// template via <see cref="IEmailTemplateService"/>, builds the <c>/join/{slug}/{token}</c>
/// link from <see cref="EmailOptions.FrontendBaseUrl"/>, and hands the HTML to
/// <see cref="IEmailSender"/> (Mailpit locally, Resend on Dev/Prod). No new infrastructure.
/// </summary>
public sealed class TeamEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;

    public TeamEmailService(IEmailTemplateService templates, IEmailSender sender, IOptions<EmailOptions> options)
    {
        _templates = templates;
        _sender = sender;
        _options = options.Value;
    }

    public async Task SendTeamInviteEmailAsync(
        string toEmail,
        string recipientName,
        string teamName,
        string inviterName,
        string slug,
        string token,
        DateTime expiresDate,
        CancellationToken ct = default)
    {
        var url = BuildJoinLink(_options.FrontendBaseUrl, slug, token);
        var html = await _templates.GenerateInvitationEmailAsync(
            recipientName: recipientName,
            inviterName: inviterName,
            inviterEmail: string.Empty,
            organizationName: teamName,
            invitationUrl: url,
            role: "player",
            expirationDate: expiresDate);

        await _sender.SendAsync(toEmail, $"You're invited to join {teamName} — JuggerHub", html, ct);
    }

    internal static string BuildJoinLink(string frontendBaseUrl, string slug, string token)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/join/{Uri.EscapeDataString(slug)}/{Uri.EscapeDataString(token)}";
    }
}
