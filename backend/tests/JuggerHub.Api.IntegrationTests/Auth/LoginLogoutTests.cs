using System.Net;
using System.Text.RegularExpressions;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>US2 — sign-in/out, the verify gate, generic failures, and lockout.</summary>
[Collection("Auth")]
public sealed class LoginLogoutTests
{
    private readonly JuggerHubApiFactory _factory;

    public LoginLogoutTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_after_verification_succeeds_and_sets_httponly_cookies()
    {
        var client = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);

        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var setCookies = login.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(setCookies, c => c.StartsWith("jh_access="));
        Assert.Contains(setCookies, c => c.StartsWith("jh_refresh="));
        Assert.All(setCookies.Where(c => c.StartsWith("jh_")),
            c => Assert.Contains("httponly", c, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_unverified_with_correct_password_returns_403_verify()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();
        await AuthTestHelpers.RegisterAsync(client, email); // not verified

        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains("email_not_verified", await login.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_wrong_password_and_unknown_email_are_indistinguishable_401()
    {
        var client = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);

        var wrongPassword = await AuthTestHelpers.LoginAsync(client, email, "Wr0ng!Password");
        var unknownEmail = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.NewEmail(), "Wr0ng!Password");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        // Identical apart from the per-request traceId (random for both → not an oracle).
        Assert.Equal(
            StripTraceId(await wrongPassword.Content.ReadAsStringAsync()),
            StripTraceId(await unknownEmail.Content.ReadAsStringAsync()));
    }

    private static string StripTraceId(string body) =>
        Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"_\"");

    [Fact]
    public async Task Login_locks_out_after_five_failures()
    {
        var client = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);

        for (var i = 0; i < 5; i++)
        {
            await AuthTestHelpers.LoginAsync(client, email, "Wr0ng!Password");
        }

        // A 6th attempt with the CORRECT password is still refused — account locked.
        var locked = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task Me_returns_user_when_authenticated_and_401_after_logout()
    {
        var client = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);
        await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);

        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains(email, await me.Content.ReadAsStringAsync());

        var logout = await client.PostAsync("/api/v1/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var meAfter = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
    }
}
