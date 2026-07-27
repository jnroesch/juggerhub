using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// City type-ahead endpoint (feature 030, R8): <c>GET /api/v1/cities/search</c>. Queries the bundled
/// <c>CityReference</c> table (a small fixture in tests). Verifies the too-short "keep typing" state,
/// a real result shape, and the auth gate (feature 026).
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
        Assert.Equal("Germany", berlin.GetProperty("countryName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(berlin.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task Unique_city_label_omits_the_region()
    {
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var resp = await client.GetAsync("/api/v1/cities/search?q=berlin");

        var items = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var berlin = items.EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Berlin");
        Assert.Equal("Berlin, Germany", berlin.GetProperty("label").GetString());
    }

    [Fact]
    public async Task Same_name_same_country_cities_include_the_region_to_disambiguate()
    {
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var resp = await client.GetAsync("/api/v1/cities/search?q=springfield");

        var labels = (await resp.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Where(c => c.GetProperty("name").GetString() == "Springfield")
            .Select(c => c.GetProperty("label").GetString())
            .ToList();
        Assert.Contains("Springfield, Illinois, United States", labels);
        Assert.Contains("Springfield, Missouri, United States", labels);
    }

    [Fact]
    public async Task Anonymous_search_is_rejected()
    {
        var resp = await _factory.CreateClient().GetAsync("/api/v1/cities/search?q=berlin");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Countries_lists_from_the_reference_dataset_not_just_located_records()
    {
        // No home city / team is set up here on purpose: countries come from the reference dataset,
        // so a country is offered even with zero located records (the results' empty state handles it).
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var resp = await client.GetAsync("/api/v1/cities/countries");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var items = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var germany = items.EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Germany");
        Assert.Equal("DE", germany.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Anonymous_countries_is_rejected()
    {
        var resp = await _factory.CreateClient().GetAsync("/api/v1/cities/countries");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
