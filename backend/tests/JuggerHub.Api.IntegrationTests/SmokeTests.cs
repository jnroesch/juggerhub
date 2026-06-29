namespace JuggerHub.Api.IntegrationTests;

/// <summary>
/// Trivial harness smoke test: proves the integration-test project, the
/// Testcontainers Postgres, and the WebApplicationFactory boot together (the app
/// auto-migrates against the container on startup). The real slice assertions
/// live in HealthEndpointTests / DiagnosticsEndpointTests.
/// </summary>
public sealed class SmokeTests : IClassFixture<JuggerHubApiFactory>
{
    private readonly JuggerHubApiFactory _factory;

    public SmokeTests(JuggerHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Application_boots_against_a_real_database()
    {
        var client = _factory.CreateClient();
        Assert.NotNull(client);
    }
}
