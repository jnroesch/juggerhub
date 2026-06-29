using System.Net;

namespace JuggerHub.Api.IntegrationTests;

/// <summary>
/// US2 — proves the auth boundary is enforced server-side: the protected sample
/// endpoint rejects unauthenticated requests with 401 and a generic
/// ProblemDetails body (no stack trace / secret), while public health still
/// succeeds.
/// </summary>
public sealed class DiagnosticsEndpointTests : IClassFixture<JuggerHubApiFactory>
{
    private readonly JuggerHubApiFactory _factory;

    public DiagnosticsEndpointTests(JuggerHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Whoami_without_credentials_returns_401_problem_details()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/diagnostics/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":401", body);
        // No internal detail / secret leakage.
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SigningKey", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Public_health_still_succeeds_without_credentials()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
