using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>US3 — forgot/reset, enumeration neutrality, and session invalidation.</summary>
[Collection("Auth")]
public sealed class ForgotResetTests
{
    private const string NewPassword = "N3w!Passw0rd#";

    private readonly JuggerHubApiFactory _factory;

    public ForgotResetTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Forgot_password_is_neutral_for_known_and_unknown()
    {
        var client = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);

        var known = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = AuthTestHelpers.NewEmail() });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reset_password_updates_password_and_rejects_old_one()
    {
        var client = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);

        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        var (userId, token) = AuthTestHelpers.ParseResetLink(_factory.EmailSender.LatestFor(email)!.HtmlBody);

        var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId, token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var oldLogin = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await AuthTestHelpers.LoginAsync(client, email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Reset_password_revokes_existing_sessions()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);

        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        var refreshCookie = AuthTestHelpers.CookieValue(login, "jh_refresh")!;

        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        var (userId, token) = AuthTestHelpers.ParseResetLink(_factory.EmailSender.LatestFor(email)!.HtmlBody);
        await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId, token, newPassword = NewPassword });

        // The session held before the reset can no longer be refreshed.
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshReq.Headers.Add("Cookie", $"jh_refresh={refreshCookie}");
        var refresh = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_invalid_token_returns_400()
    {
        var client = _factory.CreateClient();
        var (userId, _) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);

        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password", new { userId, token = "invalid-token", newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }
}
