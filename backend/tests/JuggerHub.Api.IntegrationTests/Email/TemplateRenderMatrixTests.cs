using JuggerHub.Common;
using JuggerHub.Services;
using JuggerHub.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JuggerHub.Api.IntegrationTests.Email;

/// <summary>
/// Renders each of the four emails introduced by feature 039 in each supported language — the full
/// 4 × 3 matrix behind SC-002 — through the real <see cref="EmailTemplateService"/>.
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
        Assert.Contains("Benachrichtigungen verwalten", de, StringComparison.Ordinal);
        Assert.Contains("Gestionar notificaciones", es, StringComparison.Ordinal);
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
