using System.Text.RegularExpressions;
using JuggerHub.Common;
using JuggerHub.Entities;
using JuggerHub.Services;
using JuggerHub.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JuggerHub.Api.IntegrationTests.Email;

/// <summary>
/// Renders each recipient-addressed email in each supported language through the real
/// <see cref="EmailTemplateService"/>. Began as feature 039's 4 × 3 matrix (SC-002); 058 and GH #379
/// added their emails.
///
/// Drives the service directly rather than through the API so the whole matrix is covered without a
/// database: the thing under test is the template pipeline (load → wrap in header/footer →
/// substitute → escape), and that has no persistence in it. The producer services that *call* these
/// methods are covered by the integration suites.
/// </summary>
public sealed class TemplateRenderMatrixTests
{
    private const string BaseUrl = "http://localhost:3000";

    public static TheoryData<string> Cultures => ["en", "de", "es"];

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Event_cancelled_renders(string culture)
    {
        var html = await Service().GenerateEventCancelledEmailAsync("Hamburg Autumn Open", $"{BaseUrl}/events/abc", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Hamburg Autumn Open", html, StringComparison.Ordinal);
        Assert.Contains($"{BaseUrl}/events/abc", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Party_request_renders(string culture)
    {
        var html = await Service().GeneratePartyRequestEmailAsync(
            "Mira", "Rheinfeuer", "Hamburg Autumn Open", $"{BaseUrl}/t/rf/party/abc", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Rheinfeuer", html, StringComparison.Ordinal);
        Assert.Contains($"{BaseUrl}/t/rf/party/abc", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Party_news_renders(string culture)
    {
        var html = await Service().GeneratePartyNewsEmailAsync(
            "Mira", "Rheinfeuer", "Hamburg Autumn Open", "Bring the spare chains.", $"{BaseUrl}/t/rf/party/abc", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Bring the spare chains.", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Market_invite_renders(string culture)
    {
        var html = await Service().GenerateMarketInviteEmailAsync(
            "Mira", "Rheinfeuer", "Hamburg Autumn Open", "Jonas", $"{BaseUrl}/events/abc", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Jonas", html, StringComparison.Ordinal);
    }

    // --- Feature 058: join requests ---------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Join_request_renders(string culture)
    {
        var html = await Service().GenerateJoinRequestEmailAsync(
            "Mira", "Jonas Weber", "Rheinfeuer", $"{BaseUrl}/t/rf", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Jonas Weber", html, StringComparison.Ordinal);
        Assert.Contains("Rheinfeuer", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/t/rf\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Join_request_accepted_renders(string culture)
    {
        var html = await Service().GenerateJoinRequestAcceptedEmailAsync("Jonas", "Rheinfeuer", $"{BaseUrl}/t/rf", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Rheinfeuer", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/t/rf\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Join_request_declined_renders(string culture)
    {
        var html = await Service().GenerateJoinRequestDeclinedEmailAsync("Jonas", "Rheinfeuer", $"{BaseUrl}/browse/teams", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Rheinfeuer", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/browse/teams\"", html, StringComparison.Ordinal);
    }

    // --- Feature 064: departures ------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Removed_from_team_renders(string culture)
    {
        var html = await Service().GenerateRemovedFromTeamEmailAsync("Jonas", "Rheinfeuer", $"{BaseUrl}/t/rf", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Rheinfeuer", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/t/rf\"", html, StringComparison.Ordinal);
        // The team and nobody else: no slot for an admin or another player exists (spec FR-011).
        Assert.DoesNotContain("PLAYER_NAME", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Member_left_renders(string culture)
    {
        var html = await Service().GenerateMemberLeftEmailAsync("Mira", "Jonas Weber", "Rheinfeuer", $"{BaseUrl}/t/rf", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Jonas Weber", html, StringComparison.Ordinal);
        Assert.Contains("Rheinfeuer", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/t/rf\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Member_removed_renders(string culture)
    {
        var html = await Service().GenerateMemberRemovedEmailAsync("Mira", "Jonas Weber", "Rheinfeuer", $"{BaseUrl}/t/rf", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Jonas Weber", html, StringComparison.Ordinal);
        Assert.Contains("Rheinfeuer", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/t/rf\"", html, StringComparison.Ordinal);
    }

    // --- GH #379: the five emails that were English for everyone ------------------------------

    private static readonly DateTime Expiry = new(2026, 10, 6, 14, 30, 0, DateTimeKind.Utc);

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Team_invite_renders(string culture)
    {
        var html = await Service().GenerateTeamInviteEmailAsync("Jonas", "Rheinfeuer", $"{BaseUrl}/join/rf/tok", Expiry, culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Rheinfeuer", html, StringComparison.Ordinal);
        Assert.Contains("Jonas", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/join/rf/tok\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Event_admin_invite_renders(string culture)
    {
        var html = await Service().GenerateEventAdminInviteEmailAsync("Jonas", "Hamburg Autumn Open", $"{BaseUrl}/event-invite/tok", Expiry, culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Hamburg Autumn Open", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/event-invite/tok\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Party_admin_invite_renders(string culture)
    {
        var html = await Service().GeneratePartyAdminInviteEmailAsync(
            "Jonas", "Rheinfeuer", "Hamburg Autumn Open", $"{BaseUrl}/party-invite/tok", Expiry, culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Rheinfeuer @ Hamburg Autumn Open", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/party-invite/tok\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Team_role_changed_renders(string culture)
    {
        var html = await Service().GenerateTeamRoleChangedEmailAsync("Rheinfeuer", $"{BaseUrl}/t/rf", "Jonas", TeamRole.Admin, culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Jonas", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/t/rf\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Team_news_renders(string culture)
    {
        var html = await Service().GenerateTeamNewsEmailAsync("Rheinfeuer", $"{BaseUrl}/t/rf", "Jonas", "Bring the spare chains.", culture);
        AssertWellFormed(html, culture);
        Assert.Contains("Bring the spare chains.", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/t/rf\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Only English spells the month. The app runs globalization-invariant, so a month-name pattern
    /// would print "October" inside a German or Spanish sentence.
    /// </summary>
    [Theory]
    [InlineData("en", "October 06, 2026 at 14:30 UTC")]
    [InlineData("de", "am 06.10.2026 um 14:30 UTC")]
    [InlineData("es", "el 06/10/2026 a las 14:30 UTC")]
    public async Task An_invites_expiry_reads_in_the_recipients_language(string culture, string expected)
    {
        var html = await Service().GenerateTeamInviteEmailAsync("Jonas", "Rheinfeuer", $"{BaseUrl}/join/rf/tok", Expiry, culture);
        Assert.Contains(expected, html, StringComparison.Ordinal);
    }

    /// <summary>The emails that were already translated carry their dates the same way.</summary>
    [Theory]
    [InlineData("en", "October 06, 2026")]
    [InlineData("de", "06.10.2026")]
    [InlineData("es", "06/10/2026")]
    public async Task Every_dated_email_writes_the_date_in_the_recipients_language(string culture, string expected)
    {
        var service = Service();
        var emails = new[]
        {
            await service.GenerateWelcomeEmailAsync("Mira", "mira@example.com", "JuggerHub", Expiry, culture),
            await service.GeneratePasswordChangeNotificationEmailAsync("Mira", "mira@example.com", Expiry, "203.0.113.7", culture),
            await service.GenerateAccountDeletedEmailAsync("Mira", "mira@example.com", Expiry, culture),
        };

        Assert.All(emails, html => Assert.Contains(expected, html, StringComparison.Ordinal));
        if (culture != "en")
        {
            Assert.All(emails, html => Assert.DoesNotContain("October", html, StringComparison.Ordinal));
        }
    }

    /// <summary>The role reaches the reader in words of their language, never as the enum's name.</summary>
    [Theory]
    [InlineData("en", TeamRole.Admin, "You're now an admin of Rheinfeuer.", "Admin")]
    [InlineData("en", TeamRole.Member, "You're now a member of Rheinfeuer.", "Member")]
    [InlineData("de", TeamRole.Admin, "Du bist jetzt Admin von Rheinfeuer.", "Admin")]
    [InlineData("de", TeamRole.Member, "Du bist jetzt Mitglied von Rheinfeuer.", "Mitglied")]
    [InlineData("es", TeamRole.Admin, "Ahora eres admin de Rheinfeuer.", "Admin")]
    [InlineData("es", TeamRole.Member, "Ahora eres miembro de Rheinfeuer.", "Miembro")]
    public async Task The_role_change_names_the_role_in_the_recipients_language(
        string culture, TeamRole role, string sentence, string badge)
    {
        var html = await Service().GenerateTeamRoleChangedEmailAsync("Rheinfeuer", $"{BaseUrl}/t/rf", "Jonas", role, culture);
        Assert.Contains(sentence, html, StringComparison.Ordinal);
        Assert.Contains($">{badge}</span>", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A name that cannot be resolved (banned or erased) falls back to the product's placeholder in
    /// the reader's language, not to an English word the server made up.
    /// </summary>
    [Fact]
    public async Task A_missing_author_reads_as_the_placeholder_in_the_readers_language()
    {
        var html = await Service().GenerateTeamNewsEmailAsync("Rheinfeuer", $"{BaseUrl}/t/rf", null, "Bring the spare chains.", "de");
        Assert.Contains(MemberPlaceholder.For("de"), html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_inviters_name_is_escaped_in_the_invite()
    {
        // The inviter chose their display name; it reaches another player's mailbox (FR-006).
        var html = await Service().GenerateTeamInviteEmailAsync("<img src=x>", "Rheinfeuer", $"{BaseUrl}/join/rf/tok", Expiry, "de");

        Assert.DoesNotContain("<img src=x>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;img src=x&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_players_name_is_escaped_in_the_admins_email()
    {
        // The player chose their display name; it reaches another member's mailbox (FR-006).
        var html = await Service().GenerateJoinRequestEmailAsync(
            "Mira", "<img src=x>", "Rheinfeuer", $"{BaseUrl}/t/rf", "en");

        Assert.DoesNotContain("<img src=x>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;img src=x&gt;", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Accented characters survive the escaping pass. This is the assertion that would have caught
    /// the first implementation, which used an encoder that turned every non-ASCII character into a
    /// numeric entity — visually identical in a mail client, unreadable in the source.
    /// </summary>
    [Fact]
    public async Task German_and_spanish_bodies_keep_their_accented_characters()
    {
        var de = await Service().GeneratePartyRequestEmailAsync(
            "Mira", "Rheinfeuer", "Hamburg Autumn Open", $"{BaseUrl}/t/rf/party/abc", "de");
        var es = await Service().GeneratePartyRequestEmailAsync(
            "Mira", "Rheinfeuer", "Hamburg Autumn Open", $"{BaseUrl}/t/rf/party/abc", "es");

        Assert.DoesNotContain("&#2", de, StringComparison.Ordinal);
        Assert.DoesNotContain("&#2", es, StringComparison.Ordinal);
        Assert.Contains("Meldungen verwalten", de, StringComparison.Ordinal);
        Assert.Contains("Gestionar notificaciones", es, StringComparison.Ordinal);
    }

    /// <summary>
    /// A link with query parameters survives rendering intact, readable straight out of the raw
    /// HTML — no <c>&amp;amp;</c> in place of the ampersand.
    ///
    /// This pins a regression that the unit and integration suites both missed and only the e2e run
    /// caught. Encode-by-default originally applied to URLs too, which rewrote
    /// <c>?userId=…&amp;token=…</c> as <c>?userId=…&amp;amp;token=…</c>. A browser resolves that
    /// correctly, so clicking the link by hand still worked — but every consumer that reads the
    /// HTML as text saw a parameter named <c>amp;token</c>, lost the real token, and got
    /// "this verification link is invalid or has expired". Registration broke everywhere.
    ///
    /// The extraction below is deliberately the same regex the e2e suite uses, so this test fails
    /// for the same reason the e2e would.
    /// </summary>
    [Fact]
    public async Task Verification_link_query_string_survives_rendering()
    {
        const string url = $"{BaseUrl}/verify-email?userId=0198c4f2-0000-7000-8000-000000000001&token=CfDJ8AbC%2Fd%2Be";

        var html = await Service().GenerateEmailVerificationEmailAsync("Mira", "mira@example.com", url);

        Assert.DoesNotContain("&amp;token=", html, StringComparison.Ordinal);

        var match = Regex.Match(html, @"https?://[^""'\s]*/verify-email\?[^""'\s<]+");
        Assert.True(match.Success, "No verification link could be extracted from the rendered email.");

        var query = System.Web.HttpUtility.ParseQueryString(new Uri(match.Value).Query);
        Assert.Equal("0198c4f2-0000-7000-8000-000000000001", query["userId"]);
        Assert.False(string.IsNullOrEmpty(query["token"]), "The token parameter was lost in rendering.");
        Assert.Null(query["amp;token"]);
    }

    /// <summary>Every link in every new template stays navigable from the raw source.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Links_are_not_entity_escaped(string culture)
    {
        var html = await Service().GeneratePartyRequestEmailAsync(
            "Mira", "Rheinfeuer", "Hamburg Autumn Open", $"{BaseUrl}/t/rf/party/abc?ref=email&src=cta", culture);

        Assert.Contains("?ref=email&src=cta", html, StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;src=cta", html, StringComparison.Ordinal);
    }

    /// <summary>Markup in a user-supplied value never reaches the reader as markup (FR-006).</summary>
    [Fact]
    public async Task User_supplied_markup_is_escaped_in_every_language()
    {
        foreach (var culture in new[] { "en", "de", "es" })
        {
            var html = await Service().GeneratePartyRequestEmailAsync(
                "Mira", "<b>Ravens</b>", "Hamburg Autumn Open", $"{BaseUrl}/t/rf/party/abc", culture);

            Assert.DoesNotContain("<b>Ravens</b>", html, StringComparison.Ordinal);
            Assert.Contains("&lt;b&gt;Ravens&lt;/b&gt;", html, StringComparison.Ordinal);
        }
    }

    private static void AssertWellFormed(string html, string culture)
    {
        // Nothing unsubstituted (FR-026).
        Assert.DoesNotContain("{{", html, StringComparison.Ordinal);

        // Shared chrome present (FR-001/FR-002).
        Assert.Contains("class=\"header\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"footer\"", html, StringComparison.Ordinal);
        Assert.Contains("footer-reason", html, StringComparison.Ordinal);

        // Legal links present and on the configured host (FR-022/FR-023).
        Assert.Contains($"href=\"{BaseUrl}/privacy\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/imprint\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/settings/notifications\"", html, StringComparison.Ordinal);

        // No message ends on the bare sign-off the hand-rolled bodies used (SC-001).
        Assert.DoesNotContain("<p>— JuggerHub</p>", html, StringComparison.Ordinal);

        _ = culture;
    }

    private static EmailTemplateService Service() => new(
        new TemplateRootEnvironment(BackendRoot()),
        NullLogger<EmailTemplateService>.Instance,
        Options.Create(new EmailOptions { FrontendBaseUrl = BaseUrl }),
        new EmailLocalizer());

    /// <summary>Walks up from the test output to the backend project root that owns EmailTemplates.</summary>
    private static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "EmailTemplates", "en")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the backend root containing EmailTemplates.");
    }

    /// <summary>Minimal host environment — the template service only reads ContentRootPath.</summary>
    private sealed class TemplateRootEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "JuggerHub.Api";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Test";
    }
}
