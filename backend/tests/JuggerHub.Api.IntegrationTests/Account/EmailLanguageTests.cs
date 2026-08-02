using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Account;

/// <summary>
/// Recipient-addressed emails render in the recipient's stored language (feature 031, FR-012):
/// a user who chose German gets a German password-reset email — subject and body — even though the
/// forgot-password request itself is anonymous. Reuses the "Teams" fixture + real mail sink.
/// </summary>
[Collection("Teams")]
public sealed class EmailLanguageTests
{
    private readonly JuggerHubApiFactory _factory;

    public EmailLanguageTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Password_reset_email_uses_the_recipients_stored_language()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();

        // Recipient chooses German.
        await client.PutAsJsonAsync("/api/v1/account/language", new { language = "de" });

        _factory.EmailSender.Clear();
        var forgot = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        forgot.EnsureSuccessStatusCode();

        var mail = _factory.EmailSender.LatestFor(email);
        Assert.NotNull(mail);
        Assert.Equal("Setze dein Passwort zurück — JuggerHub", mail!.Subject);
        Assert.Contains("Passwort zurücksetzen", mail.HtmlBody);
        Assert.DoesNotContain("Reset my password", mail.HtmlBody);
    }

    /// <summary>
    /// The four emails migrated by feature 039 follow the same rule (FR-009). Before that they were
    /// hardcoded English regardless of what the recipient had chosen — a team invite stands in here
    /// for the shared chrome path, and the party/market emails are covered in their own suites.
    /// </summary>
    [Fact]
    public async Task Non_auth_email_chrome_uses_the_recipients_stored_language()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();

        await client.PutAsJsonAsync("/api/v1/account/language", new { language = "de" });

        _factory.EmailSender.Clear();
        var forgot = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        forgot.EnsureSuccessStatusCode();

        var mail = _factory.EmailSender.LatestFor(email);
        Assert.NotNull(mail);

        // German footer chrome — the reason line and the manage-notifications label come from the
        // de/ footer, so this proves the whole wrapper resolved to the recipient's language.
        //
        // The umlaut is asserted literally on purpose: the template layer escapes markup but must
        // leave accented characters alone, or every German and Spanish email turns into numeric
        // entities. This assertion is what pins that.
        Assert.Contains("Du erhältst diese E-Mail", mail!.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Benachrichtigungen verwalten", mail.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("getting this because", mail.HtmlBody, StringComparison.Ordinal);
    }
}
