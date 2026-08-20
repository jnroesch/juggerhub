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
    public async Task Showcase_reads_follow_the_two_surfaces_existing_rules()
    {
        // Feature 046 (#99) adds two anonymous-capable endpoints and eight authenticated ones. This
        // is the record of that split: a profile's showcase is reachable exactly where its avatar is
        // (the service still gates each request on the owner's public/private choice), while the
        // team surface gains nothing anonymous at all.
        var anon = _factory.CreateClient();

        // Anonymous-CAPABLE: refused with 404 rather than 401, because a private or unknown profile
        // is deliberately indistinguishable — the gate lives in the service, not in the pipeline.
        Assert.Equal(HttpStatusCode.NotFound,
            (await anon.GetAsync("/api/v1/profiles/nobodyatall/showcase")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await anon.GetAsync($"/api/v1/profiles/nobodyatall/showcase/{Guid.CreateVersion7()}/image")).StatusCode);

        // Team showcase: authenticated-only, like the rest of the team surface (feature 026).
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/v1/teams/no-such-team/showcase")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync($"/api/v1/teams/no-such-team/showcase/{Guid.CreateVersion7()}/image")).StatusCode);

        // And no showcase WRITE is anonymous on either surface.
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent([1, 2, 3]), "file", "x.png");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsync("/api/v1/profiles/me/showcase", upload)).StatusCode);
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
