using System.Net;
using JuggerHub.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        var oldRefresh = AuthTestHelpers.CookieValue(login, "jh_refresh")!;

        var first = await SendRefresh(client, oldRefresh);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var newRefresh = AuthTestHelpers.CookieValue(first, "jh_refresh")!;

        // Move the rotation out of the lost-race grace window (#247): a replay seconds later is a
        // second tab; this one is theft.
        await BackdateRotationAsync(userId);

        // Replaying the OLD (rotated) token = reuse → family revoked, 401.
        var reuse = await SendRefresh(client, oldRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // The NEW token is now dead too (whole family revoked).
        var afterReuse = await SendRefresh(client, newRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task A_second_tab_presenting_the_just_rotated_token_gets_an_access_cookie_only()
    {
        var client = ManualClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        var shared = AuthTestHelpers.CookieValue(login, "jh_refresh")!;

        var winner = await SendRefresh(client, shared);
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        var successor = AuthTestHelpers.CookieValue(winner, "jh_refresh")!;

        // The other tab sent the same cookie before the winner's response landed.
        var loser = await SendRefresh(client, shared);

        Assert.Equal(HttpStatusCode.OK, loser.StatusCode);
        Assert.False(string.IsNullOrEmpty(AuthTestHelpers.CookieValue(loser, "jh_access")));
        // No refresh cookie at all — not a new one, and not a deletion that would wipe the winner's.
        Assert.DoesNotContain(loser.Headers.GetValues("Set-Cookie"), c => c.StartsWith("jh_refresh=", StringComparison.Ordinal));

        // The family was NOT revoked: the winner's successor keeps working.
        var next = await SendRefresh(client, successor);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task Concurrent_refreshes_with_one_token_produce_exactly_one_successor()
    {
        var client = ManualClient();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        var shared = AuthTestHelpers.CookieValue(login, "jh_refresh")!;

        // Whether these collide in the database or merely follow one another within milliseconds,
        // the property is the same — which is why it is asserted on outcomes, not on interleaving.
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => SendRefresh(client, shared)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var refreshCookies = responses
            .Select(r => AuthTestHelpers.CookieValue(r, "jh_refresh"))
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
        Assert.Single(refreshCookies);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // The login's token plus exactly one replacement.
        Assert.Equal(2, await db.RefreshTokens.CountAsync(t => t.UserId == userId));

        var next = await SendRefresh(client, refreshCookies[0]!);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    /// <summary>Pretend the rotation happened long enough ago that a replay is no longer a lost race.</summary>
    private async Task BackdateRotationAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var longAgo = DateTime.UtcNow - TimeSpan.FromMinutes(5);
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedReason == "rotated")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, longAgo));
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
