namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>
/// Every link in an outgoing email must come from <c>Email:FrontendBaseUrl</c> — the one
/// setting each environment already configures. These pin a regression: the template layer
/// used to read a key (<c>EmailSettings:FrontendUrl</c>) that is configured nowhere, so
/// every call-to-action fell through to a hardcoded <c>app.juggerhub.com</c> — a host this
/// project does not run.
/// </summary>
[Collection("Auth")]
public sealed class EmailLinkTests
{
    /// <summary>What <see cref="JuggerHubApiFactory"/> configures as the SPA base URL.</summary>
    private const string BaseUrl = "http://localhost:3000";

    private readonly JuggerHubApiFactory _factory;

    public EmailLinkTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Welcome_email_call_to_action_points_at_the_configured_frontend()
    {
        var client = _factory.CreateClient();

        // Verifying the address is what triggers the welcome email, so it is the latest capture.
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);

        var welcome = _factory.EmailSender.LatestFor(email);
        Assert.NotNull(welcome);
        Assert.Contains("Welcome", welcome!.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"href=\"{BaseUrl}\"", welcome.HtmlBody);
        Assert.DoesNotContain("app.juggerhub.com", welcome.HtmlBody);
    }

    [Fact]
    public async Task Footer_notification_link_points_at_the_configured_frontend()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        await AuthTestHelpers.RegisterAsync(client, email);

        // The footer is shared by every template, so any captured email proves it.
        var mail = _factory.EmailSender.LatestFor(email);
        Assert.NotNull(mail);
        Assert.Contains($"href=\"{BaseUrl}/settings/notifications\"", mail!.HtmlBody);
        Assert.DoesNotContain("https://juggerhub.com/settings", mail.HtmlBody);
    }

    [Fact]
    public async Task No_email_leaves_a_placeholder_unrendered()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        await AuthTestHelpers.RegisterAsync(client, email);

        var mail = _factory.EmailSender.LatestFor(email);
        Assert.NotNull(mail);
        Assert.DoesNotContain("{{", mail!.HtmlBody);
    }

    /// <summary>
    /// The shared footer carries the privacy policy and imprint (feature 039, FR-022/FR-023). Both
    /// are built from the same configured host as every other link, so an email can never send a
    /// reader to a different origin than the one beside it.
    /// </summary>
    [Fact]
    public async Task Footer_carries_privacy_and_imprint_links_on_the_configured_frontend()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        await AuthTestHelpers.RegisterAsync(client, email);

        var mail = _factory.EmailSender.LatestFor(email);
        Assert.NotNull(mail);
        Assert.Contains($"href=\"{BaseUrl}/privacy\"", mail!.HtmlBody, StringComparison.Ordinal);
        Assert.Contains($"href=\"{BaseUrl}/imprint\"", mail.HtmlBody, StringComparison.Ordinal);
    }
}
