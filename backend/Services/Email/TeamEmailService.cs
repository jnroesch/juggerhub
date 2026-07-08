using JuggerHub.Common;
using JuggerHub.Entities;
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

    /// <summary>Team role-change email (feature 011), gated by the recipient's Email preference.</summary>
    public async Task SendRoleChangedEmailAsync(
        string toEmail, string teamName, string slug, string? actorName, TeamRole newRole, CancellationToken ct = default)
    {
        var url = BuildTeamLink(_options.FrontendBaseUrl, slug);
        var rolePhrase = newRole == TeamRole.Admin ? "an admin" : "a member";
        var html = await _templates.GenerateTeamRoleChangedEmailAsync(teamName, url, actorName, newRole.ToString(), rolePhrase);
        await _sender.SendAsync(toEmail, $"Your role in {teamName} changed — JuggerHub", html, ct);
    }

    /// <summary>Team-news email (feature 011), sent to each member whose Email preference is on.</summary>
    public async Task SendTeamNewsEmailAsync(
        string toEmail, string teamName, string slug, string? authorName, string excerpt, CancellationToken ct = default)
    {
        var url = BuildTeamLink(_options.FrontendBaseUrl, slug);
        var html = await _templates.GenerateTeamNewsEmailAsync(teamName, url, authorName, excerpt);
        await _sender.SendAsync(toEmail, $"News from {teamName} — JuggerHub", html, ct);
    }

    internal static string BuildJoinLink(string frontendBaseUrl, string slug, string token)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/join/{Uri.EscapeDataString(slug)}/{Uri.EscapeDataString(token)}";
    }

    internal static string BuildTeamLink(string frontendBaseUrl, string slug) =>
        $"{frontendBaseUrl.TrimEnd('/')}/t/{Uri.EscapeDataString(slug)}";
}
