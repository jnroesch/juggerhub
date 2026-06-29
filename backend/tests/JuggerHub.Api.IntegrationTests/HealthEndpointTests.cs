using System.Net;
using System.Net.Http.Json;

namespace JuggerHub.Api.IntegrationTests;

/// <summary>
/// US1 — proves the end-to-end slice against a real (Testcontainers) Postgres:
/// the public health endpoint returns 200 and reports the database reachable.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<JuggerHubApiFactory>
{
    private readonly JuggerHubApiFactory _factory;

    public HealthEndpointTests(JuggerHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_200_and_database_reachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotNull(body);
        Assert.Equal("healthy", body!.Status);
        Assert.Equal("reachable", body.Database);
        Assert.False(string.IsNullOrWhiteSpace(body.Version));
    }

    private sealed record HealthResponse(string Status, string Database, string Version, DateTime Timestamp);
}
