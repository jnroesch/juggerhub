using System.Net;

namespace JuggerHub.Api.IntegrationTests;

/// <summary>
/// US3 — in Development the OpenAPI document and the Scalar interactive API
/// reference are browsable (SC-008). The factory runs in the Development
/// environment.
/// </summary>
public sealed class ApiDocsTests : IClassFixture<JuggerHubApiFactory>
{
    private readonly JuggerHubApiFactory _factory;

    public ApiDocsTests(JuggerHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/health", body);
        Assert.Contains("/api/v1/diagnostics/whoami", body);
    }

    [Fact]
    public async Task Scalar_reference_ui_is_served()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
