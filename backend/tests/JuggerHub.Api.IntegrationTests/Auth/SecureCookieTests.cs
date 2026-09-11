using System.Net;
using System.Net.Http.Json;
using JuggerHub.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>
/// GH #246 — the auth cookies' <c>Secure</c> attribute. It used to follow the environment NAME, so
/// the internet-facing Dev deployment (which runs as <c>Development</c>) issued its session cookies
/// without it. It is now configuration that defaults to secure.
/// </summary>
/// <remarks>
/// The shared test host turns Secure off (plain http, like docker-compose); these tests build their
/// own host to see what a deployment — which sets nothing — actually gets.
/// </remarks>
[Collection("Auth")]
public sealed class SecureCookieTests
{
    private readonly JuggerHubApiFactory _factory;

    public SecureCookieTests(JuggerHubApiFactory factory) => _factory = factory;

    /// <summary>
    /// What every deployed environment runs with — they configure nothing. Asserted on the options
    /// type because a layered test host cannot UN-set the shared host's <c>false</c> (a later null
    /// value falls back to the earlier one); the Dev deployment is the end-to-end check.
    /// </summary>
    [Fact]
    public void Secure_is_the_default()
    {
        Assert.True(new AuthCookieOptions().Secure);
    }

    [Fact]
    public async Task Session_cookies_are_Secure_when_configured_so()
    {
        var login = await LoginThroughHostAsync("true");

        Assert.Contains("secure", SetCookie(login, AuthCookieDefaults.AccessTokenCookie), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", SetCookie(login, AuthCookieDefaults.RefreshTokenCookie), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_local_stack_setting_drops_Secure()
    {
        var login = await LoginThroughHostAsync("false");

        Assert.DoesNotContain("secure", SetCookie(login, AuthCookieDefaults.AccessTokenCookie), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", SetCookie(login, AuthCookieDefaults.RefreshTokenCookie), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Account_deletion_clears_the_refresh_cookie_at_its_own_path()
    {
        // Before #246 the deletion endpoint called a bare Delete(), which targets path "/" — the
        // browser only removes a cookie whose path matches, so the refresh cookie survived.
        await using var host = HostWith("true");
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(_factory.CreateClient(), _factory);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        var access = AuthTestHelpers.CookieValue(login, AuthCookieDefaults.AccessTokenCookie)!;

        using var delete = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/deletion")
        {
            Content = JsonContent.Create(new { password = AuthTestHelpers.ValidPassword, confirmation = "DELETE" }),
        };
        delete.Headers.Add("Cookie", $"{AuthCookieDefaults.AccessTokenCookie}={access}");
        var deleted = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var refresh = SetCookie(deleted, AuthCookieDefaults.RefreshTokenCookie);
        Assert.Contains($"path={AuthCookieDefaults.RefreshTokenPath}", refresh, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=Thu, 01 Jan 1970", refresh, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", refresh, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpResponseMessage> LoginThroughHostAsync(string secure)
    {
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(_factory.CreateClient(), _factory);
        await using var host = HostWith(secure);
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return login;
    }

    /// <summary>A host whose Auth:Cookies:Secure is <paramref name="secure"/>.</summary>
    private WebApplicationFactory<Program> HostWith(string secure) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Cookies:Secure"] = secure })));

    private static string SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(name + "=", StringComparison.Ordinal));
}
