using System.Net;
using System.Net.Http.Json;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>US1 — registration + email verification, including enumeration neutrality.</summary>
[Collection("Auth")]
public sealed class RegisterVerifyTests
{
    private readonly JuggerHubApiFactory _factory;

    public RegisterVerifyTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_new_email_returns_200_and_sends_verification_email()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        var response = await AuthTestHelpers.RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mail = _factory.EmailSender.LatestFor(email);
        Assert.NotNull(mail);
        Assert.Contains("verify-email", mail!.HtmlBody);
    }

    [Fact]
    public async Task Verification_email_greets_the_player_by_handle_not_email_prefix()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();
        var handle = AuthTestHelpers.NewHandle();

        await AuthTestHelpers.RegisterAsync(client, email, handle: handle);

        // The address itself is in the body too, so assert on the greeting, not on absence.
        var html = _factory.EmailSender.LatestFor(email)!.HtmlBody;
        Assert.Contains($"Almost there, {handle}.", html);
        Assert.DoesNotContain($"Almost there, {email.Split('@')[0]}.", html);
    }

    [Fact]
    public async Task Welcome_email_greets_the_player_by_handle_not_email_prefix()
    {
        // Verification reloads the account by id, so unlike registration the profile is not in
        // memory — this covers the lookup path.
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();

        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);

        var html = _factory.EmailSender.LatestFor(email)!.HtmlBody;
        Assert.Contains($"in, {handle}.", html);
        Assert.DoesNotContain($"in, {email.Split('@')[0]}.", html);
    }

    [Fact]
    public async Task Register_existing_email_returns_identical_neutral_response()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        var first = await AuthTestHelpers.RegisterAsync(client, email);
        var second = await AuthTestHelpers.RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        // Indistinguishable responses → no account enumeration.
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_weak_password_is_rejected_with_400()
    {
        var client = _factory.CreateClient();

        var response = await AuthTestHelpers.RegisterAsync(client, AuthTestHelpers.NewEmail(), "weak");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Verify_email_with_valid_token_confirms_account()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();
        await AuthTestHelpers.RegisterAsync(client, email);
        var (userId, token) = AuthTestHelpers.ParseVerificationLink(_factory.EmailSender.LatestFor(email)!.HtmlBody);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId, token });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    [Fact]
    public async Task Verify_email_with_invalid_token_returns_400()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();
        await AuthTestHelpers.RegisterAsync(client, email);
        var (userId, _) = AuthTestHelpers.ParseVerificationLink(_factory.EmailSender.LatestFor(email)!.HtmlBody);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId, token = "not-a-valid-token" });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }

    [Fact]
    public async Task Resend_verification_is_neutral_for_unknown_email()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/resend-verification", new { email = AuthTestHelpers.NewEmail() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
