using System.Net;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Security;

/// <summary>
/// Feature 026 (US3) — discovery is direct-link only. Anonymous callers keep access to the
/// intended allowlist (auth bootstrap endpoints) but have NO players/teams/events browse or
/// search surface (SC-006). The gated-read 401s are covered by <see cref="AnonymousAccessTests"/>.
/// </summary>
[Collection("Auth")]
public sealed class AnonymousAllowlistTests
{
    private readonly JuggerHubApiFactory _factory;

    public AnonymousAllowlistTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Allowlisted_bootstrap_endpoints_remain_anonymous()
    {
        var anon = _factory.CreateClient();

        // These must never require a session (registration/login UX depends on them).
        Assert.NotEqual(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/v1/auth/password-policy")).StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized,
            (await anon.GetAsync($"/api/v1/auth/handle-available?handle={AuthTestHelpers.NewHandle()}")).StatusCode);
    }

    [Fact]
    public async Task There_is_no_anonymous_players_teams_or_events_browse()
    {
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/teams")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/events")).StatusCode);
    }
}
