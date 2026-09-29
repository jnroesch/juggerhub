using JuggerHub.Common;
using JuggerHub.Entities;
using JuggerHub.Services;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Composes the team transactional emails — the invite, role change, news, join requests and
/// polls: renders each template via <see cref="IEmailTemplateService"/>, builds the links from
/// <see cref="EmailOptions.FrontendBaseUrl"/>, and hands the HTML to <see cref="IEmailSender"/>
/// (Mailpit locally, Resend on Dev/Prod). No new infrastructure.
/// </summary>
public sealed class TeamEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly IEmailLocalizer _localizer;

    public TeamEmailService(
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

    // Every email here renders in the RECIPIENT's language (the 039 pattern): it is addressed to a
    // person, not to whoever caused it. The three below were English for everyone until GH #379.

    public async Task SendTeamInviteEmailAsync(
        string toEmail, string teamName, string inviterName, string slug, string token, DateTime expiresDate,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildJoinLink(_options.FrontendBaseUrl, slug, token);
        var html = await _templates.GenerateTeamInviteEmailAsync(inviterName, teamName, url, expiresDate, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.teamInvite", culture, teamName), html, ct);
    }

    /// <summary>Team role-change email (feature 011), gated by the recipient's Email preference.</summary>
    public async Task SendRoleChangedEmailAsync(
        string toEmail, string teamName, string slug, string? actorName, TeamRole newRole,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildTeamLink(_options.FrontendBaseUrl, slug);
        var html = await _templates.GenerateTeamRoleChangedEmailAsync(teamName, url, actorName, newRole, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.teamRoleChanged", culture, teamName), html, ct);
    }

    /// <summary>Team-news email (feature 011), sent to each member whose Email preference is on.</summary>
    public async Task SendTeamNewsEmailAsync(
        string toEmail, string teamName, string slug, string? authorName, string excerpt,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildTeamLink(_options.FrontendBaseUrl, slug);
        var html = await _templates.GenerateTeamNewsEmailAsync(teamName, url, authorName, excerpt, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.teamNews", culture, teamName), html, ct);
    }

    // --- Feature 058: join requests.

    /// <summary>Tells one admin that <paramref name="playerName"/> asked to join, linking to the team page.</summary>
    public async Task SendJoinRequestEmailAsync(
        string toEmail, string recipientName, string playerName, string teamName, string slug,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildTeamLink(_options.FrontendBaseUrl, slug);
        var html = await _templates.GenerateJoinRequestEmailAsync(recipientName, playerName, teamName, url, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.joinRequest", culture, playerName, teamName), html, ct);
    }

    /// <summary>Tells the player the team accepted them, linking to the team. Names no admin.</summary>
    public async Task SendJoinRequestAcceptedEmailAsync(
        string toEmail, string recipientName, string teamName, string slug,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildTeamLink(_options.FrontendBaseUrl, slug);
        var html = await _templates.GenerateJoinRequestAcceptedEmailAsync(recipientName, teamName, url, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.joinRequestAccepted", culture, teamName), html, ct);
    }

    /// <summary>Tells the player the team declined, pointing them at other teams. Names no admin.</summary>
    public async Task SendJoinRequestDeclinedEmailAsync(
        string toEmail, string recipientName, string teamName,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildBrowseTeamsLink(_options.FrontendBaseUrl);
        var html = await _templates.GenerateJoinRequestDeclinedEmailAsync(recipientName, teamName, url, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.joinRequestDeclined", culture, teamName), html, ct);
    }

    // --- Feature 064: departures. Neither email names the admin who removed someone. ----------------

    /// <summary>Tells a player an admin removed them from a team, linking to the team page. Names the team only.</summary>
    public async Task SendRemovedFromTeamEmailAsync(
        string toEmail, string recipientName, string teamName, string slug,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildTeamLink(_options.FrontendBaseUrl, slug);
        var html = await _templates.GenerateRemovedFromTeamEmailAsync(recipientName, teamName, url, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.removedFromTeam", culture, teamName), html, ct);
    }

    /// <summary>
    /// Tells one admin that <paramref name="playerName"/> left the team (<paramref name="removed"/>
    /// false) or was removed from it (true), linking to the team page. Which admin removed them is
    /// stated nowhere.
    /// </summary>
    public async Task SendMemberDepartedEmailAsync(
        string toEmail, string recipientName, string playerName, string teamName, string slug, bool removed,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = BuildTeamLink(_options.FrontendBaseUrl, slug);
        var html = removed
            ? await _templates.GenerateMemberRemovedEmailAsync(recipientName, playerName, teamName, url, culture)
            : await _templates.GenerateMemberLeftEmailAsync(recipientName, playerName, teamName, url, culture);
        var subject = _localizer.Get(removed ? "subject.memberRemoved" : "subject.memberLeft", culture, playerName, teamName);
        await _sender.SendAsync(toEmail, subject, html, ct);
    }

    internal static string BuildBrowseTeamsLink(string frontendBaseUrl) =>
        $"{frontendBaseUrl.TrimEnd('/')}/browse/teams";

    internal static string BuildJoinLink(string frontendBaseUrl, string slug, string token)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/join/{Uri.EscapeDataString(slug)}/{Uri.EscapeDataString(token)}";
    }

    // --- Feature 062: team polls. In the recipient's language, like the join-request emails above. ----

    /// <summary>
    /// Tells one member that their team started a poll, quoting the question and linking straight to
    /// it on the team page. The subject names the team only (see <c>subject.teamPoll</c>).
    /// </summary>
    public async Task SendTeamPollEmailAsync(
        string toEmail, string recipientName, string teamName, string slug, Guid pollId, string question,
        string culture = SupportedLanguages.Default, CancellationToken ct = default)
    {
        var url = $"{BuildTeamLink(_options.FrontendBaseUrl, slug)}#poll-{pollId}";
        var html = await _templates.GenerateTeamPollEmailAsync(recipientName, teamName, question, url, culture);
        await _sender.SendAsync(toEmail, _localizer.Get("subject.teamPoll", culture, teamName), html, ct);
    }

    internal static string BuildTeamLink(string frontendBaseUrl, string slug) =>
        $"{frontendBaseUrl.TrimEnd('/')}/t/{Uri.EscapeDataString(slug)}";
}
