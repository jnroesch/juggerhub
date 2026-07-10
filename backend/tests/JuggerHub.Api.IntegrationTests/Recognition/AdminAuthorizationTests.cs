using System.Net;
using System.Net.Http.Json;

namespace JuggerHub.Api.IntegrationTests.Recognition;

/// <summary>
/// Feature 012 — the SECURITY-CRITICAL suite (constitution Principle I; spec SC-002/SC-006).
/// Every admin badge/achievement route must be refused server-side for anonymous callers (401)
/// and authenticated non-admins (403), regardless of the client UI. A designated admin is
/// permitted. Since feature 013 the boundary is membership in the <c>PlatformAdmin</c>
/// Identity role (mirrored from config at startup) — see Admin/PlatformAdminRoleSyncTests
/// for the sync lifecycle itself.
/// </summary>
[Collection("Recognition")]
public sealed class AdminAuthorizationTests
{
    private readonly JuggerHubApiFactory _factory;

    public AdminAuthorizationTests(JuggerHubApiFactory factory) => _factory = factory;

    // Every write/read admin route across both families. Ids are placeholders — authorization
    // runs before model binding, so the request is refused before the id/body ever matter.
    private static readonly (string Method, string Path)[] AdminRoutes =
    {
        ("GET", "/api/v1/admin/badges"),
        ("POST", "/api/v1/admin/badges"),
        ("PUT", $"/api/v1/admin/badges/{Guid.Empty}"),
        ("DELETE", $"/api/v1/admin/badges/{Guid.Empty}"),
        ("PUT", $"/api/v1/admin/badges/{Guid.Empty}/icon"),
        ("POST", $"/api/v1/admin/badges/{Guid.Empty}/awards"),
        ("DELETE", $"/api/v1/admin/badges/awards/{Guid.Empty}"),
        ("GET", "/api/v1/admin/achievements"),
        ("POST", "/api/v1/admin/achievements"),
        ("PUT", $"/api/v1/admin/achievements/{Guid.Empty}"),
        ("DELETE", $"/api/v1/admin/achievements/{Guid.Empty}"),
        ("PUT", $"/api/v1/admin/achievements/{Guid.Empty}/icon"),
        ("POST", $"/api/v1/admin/achievements/{Guid.Empty}/awards"),
        ("DELETE", $"/api/v1/admin/achievements/awards/{Guid.Empty}"),
        ("GET", "/api/v1/admin/access"),
        ("GET", "/api/v1/admin/players/someone/awards"),
        ("GET", "/api/v1/admin/teams/some-team/awards"),
    };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        return client.SendAsync(request);
    }

    [Fact]
    public async Task Anonymous_is_refused_401_on_every_admin_route()
    {
        var anon = _factory.CreateClient();

        foreach (var (method, path) in AdminRoutes)
        {
            var resp = await SendAsync(anon, method, path);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }
    }

    [Fact]
    public async Task Non_admin_is_refused_403_on_every_admin_route()
    {
        var (user, _, _, _) = await RecognitionTestSupport.UserClientAsync(_factory);

        foreach (var (method, path) in AdminRoutes)
        {
            var resp = await SendAsync(user, method, path);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        }
    }

    [Fact]
    public async Task Designated_admin_is_permitted()
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);

        // Positive control: the admin reaches the handler (200), not 401/403.
        var badges = await admin.GetAsync("/api/v1/admin/badges");
        var achievements = await admin.GetAsync("/api/v1/admin/achievements");

        Assert.Equal(HttpStatusCode.OK, badges.StatusCode);
        Assert.Equal(HttpStatusCode.OK, achievements.StatusCode);
    }
}
