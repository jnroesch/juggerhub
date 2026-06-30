using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>US4 — refresh rotation (single-use) and family reuse detection.</summary>
[Collection("Auth")]
public sealed class RefreshRotationTests
{
    private readonly JuggerHubApiFactory _factory;

    public RefreshRotationTests(JuggerHubApiFactory factory) => _factory = factory;

    private HttpClient ManualClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Refresh_rotates_and_issues_new_cookies()
    {
        var client = ManualClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        var oldRefresh = AuthTestHelpers.CookieValue(login, "jh_refresh")!;

        var refresh = await SendRefresh(client, oldRefresh);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var newRefresh = AuthTestHelpers.CookieValue(refresh, "jh_refresh");
        Assert.NotNull(newRefresh);
        Assert.NotEqual(oldRefresh, newRefresh);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_family()
    {
        var client = ManualClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        var oldRefresh = AuthTestHelpers.CookieValue(login, "jh_refresh")!;

        var first = await SendRefresh(client, oldRefresh);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var newRefresh = AuthTestHelpers.CookieValue(first, "jh_refresh")!;

        // Replaying the OLD (rotated) token = reuse → family revoked, 401.
        var reuse = await SendRefresh(client, oldRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // The NEW token is now dead too (whole family revoked).
        var afterReuse = await SendRefresh(client, newRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_401()
    {
        var client = ManualClient();

        var refresh = await client.PostAsync("/api/v1/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    private static Task<HttpResponseMessage> SendRefresh(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"jh_refresh={refreshToken}");
        return client.SendAsync(request);
    }
}
