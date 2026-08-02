using JuggerHub.Api.IntegrationTests.Parties;

namespace JuggerHub.Api.IntegrationTests.Email;

/// <summary>
/// The four emails migrated by feature 039 carry the same chrome as every other templated message
/// (FR-001, FR-002, FR-024).
///
/// Before this feature they were inline HTML strings that ended at a bare <c>— JuggerHub</c>: no
/// header, no branding, no address block, no footer reason, and no manage-notifications link. A
/// party request stands in for all four here — the chrome is shared, so proving it on one non-auth
/// email proves the wrapper is being applied at all, which is precisely what used to be missing.
/// </summary>
[Collection("Parties")]
public sealed class EmailChromeTests : PartyTestSupport
{
    private const string BaseUrl = "http://localhost:3000";

    public EmailChromeTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Party_request_email_carries_the_shared_chrome()
    {
        var mail = await CapturePartyRequestEmailAsync();

        // Header + branding, from header.html.
        Assert.Contains("class=\"header\"", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("brand-name", mail.HtmlBody, StringComparison.Ordinal);

        // Footer, address block, and the per-message reason line, from footer.html.
        Assert.Contains("class=\"footer\"", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("footer-address", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("footer-reason", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/settings/notifications\"", mail.HtmlBody, StringComparison.Ordinal);

        // The reason line is populated, not an empty shell. Matched without the leading "You're"
        // because the apostrophe is correctly escaped to &#x27; on the way through the template
        // layer — it renders as an apostrophe, but the raw body does not contain the literal.
        Assert.Contains("getting this because", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Party_request_email_no_longer_ends_in_a_bare_signoff()
    {
        var mail = await CapturePartyRequestEmailAsync();

        // The exact string the four hand-rolled bodies used to end on.
        Assert.DoesNotContain("<p>— JuggerHub</p>", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Party_request_email_leaves_no_unrendered_placeholder()
    {
        var mail = await CapturePartyRequestEmailAsync();

        Assert.DoesNotContain("{{", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Party_request_email_links_to_the_configured_frontend()
    {
        var mail = await CapturePartyRequestEmailAsync();

        Assert.Contains($"{BaseUrl}/t/", mail.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("app.juggerhub.com", mail.HtmlBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Forms a party on a team with one other member, and returns the request email that member got.
    /// </summary>
    private async Task<CapturedEmail> CapturePartyRequestEmailAsync()
    {
        var (adminClient, adminId, _, _) = await NewUserAsync();
        var (_, memberId, _, memberEmail) = await NewUserAsync();
        _ = adminId;

        var (teamId, _) = await CreateTeamAsync(adminClient);
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(adminClient);

        Factory.EmailSender.Clear();
        await FormPartyAsync(adminClient, eventId, teamId);

        var mail = Factory.EmailSender.LatestFor(memberEmail);
        Assert.NotNull(mail);
        return mail!;
    }
}
