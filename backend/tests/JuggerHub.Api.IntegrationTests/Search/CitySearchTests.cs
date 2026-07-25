using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// City type-ahead endpoint (feature 030): <c>GET /api/v1/cities/search</c>. Backend-proxied to the
/// geocoder (stubbed by <see cref="TestGeocoder"/>). Verifies the too-short "keep typing" state, a
/// real result shape, the graceful 503 degradation (FR-019), and the auth gate (feature 026).
/// Shares the sequential "Search" collection so toggling the shared geocoder stub is race-free.
/// </summary>
[Collection("Search")]
public sealed class CitySearchTests
{
    private readonly JuggerHubApiFactory _factory;

    public CitySearchTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Short_query_returns_empty_not_an_error()
    {
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var resp = await client.GetAsync("/api/v1/cities/search?q=b");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var items = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, items.GetArrayLength());
    }

    [Fact]
    public async Task Valid_query_returns_disambiguated_city_options()
    {
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var resp = await client.GetAsync("/api/v1/cities/search?q=ber");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var items = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var berlin = items.EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Berlin");
        Assert.Equal("TEST:berlin", berlin.GetProperty("externalId").GetString());
        Assert.Equal("Deutschland", berlin.GetProperty("countryName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(berlin.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task Geocoder_unavailable_degrades_to_503()
    {
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        _factory.Geocoder.Unavailable = true;
        try
        {
            var resp = await client.GetAsync("/api/v1/cities/search?q=berlin");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        }
        finally
        {
            _factory.Geocoder.Unavailable = false;
        }
    }

    [Fact]
    public async Task Anonymous_search_is_rejected()
    {
        var resp = await _factory.CreateClient().GetAsync("/api/v1/cities/search?q=berlin");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
