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
        var berlin = items.EnumerateArray().Single(c => c.GetProperty("externalId").GetString() == "TEST:berlin");
        Assert.Equal("Berlin", berlin.GetProperty("name").GetString());
        Assert.Equal("Germany", berlin.GetProperty("countryName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(berlin.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task Unique_city_label_omits_the_region()
    {
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var resp = await client.GetAsync("/api/v1/cities/search?q=berlin");

        var items = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // The German Berlin is unique within its country (the small US "Berlin" is a different country
        // group), so its label omits the region.
        var berlin = items.EnumerateArray().Single(c => c.GetProperty("externalId").GetString() == "TEST:berlin");
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
    public async Task Most_populous_city_ranks_first_without_home_city()
    {
        // Feature 032 (US1): a user with no home city searching a cross-country same name gets the
        // large, well-known city first — the German Berlin (~3.7M) above a tiny US "Berlin" (~20k).
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var ids = await OrderedExternalIdsAsync(client, "berlin");

        Assert.Equal("TEST:berlin", ids[0]);
        Assert.True(ids.IndexOf("TEST:berlin") < ids.IndexOf("TEST:berlin-us"));
    }

    [Fact]
    public async Task Same_name_same_country_ranked_by_population()
    {
        // Feature 032 (US1): two "Springfield, United States" — the more populous (Missouri, ~169k)
        // ranks above the smaller (Illinois, ~114k). No home city set.
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var ids = await OrderedExternalIdsAsync(client, "springfield");

        Assert.True(ids.IndexOf("TEST:springfield-mo") < ids.IndexOf("TEST:springfield-il"));
    }

    [Fact]
    public async Task Exact_name_match_outranks_alternate_name_match_despite_population()
    {
        // Feature 032 (FR-002/SC-005): the match-quality tier is never crossed by population. "Zetaville"
        // hits the term by NAME (tiny population); "Megapolis" hits it only by an ALTERNATE name (huge
        // population). The name-tier hit still comes first.
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var ids = await OrderedExternalIdsAsync(client, "zeta");

        Assert.Equal("TEST:zetaville", ids[0]);
        Assert.True(ids.IndexOf("TEST:zetaville") < ids.IndexOf("TEST:megapolis"));
    }

    [Fact]
    public async Task Ranking_is_deterministic_across_identical_requests()
    {
        // Feature 032 (FR-009): identical query, identical order.
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var first = await OrderedExternalIdsAsync(client, "berlin");
        var second = await OrderedExternalIdsAsync(client, "berlin");

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Nearby_city_outranks_distant_more_populous_city()
    {
        // Feature 032 (US2): a user whose home city (Chicago) is near Springfield, IL sees it above the
        // larger, more distant Springfield, MO — proximity outranks population within the match tier.
        var (client, userId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, userId, hometown: "Chicago");

        var ids = await OrderedExternalIdsAsync(client, "springfield");

        Assert.True(
            ids.IndexOf("TEST:springfield-il") < ids.IndexOf("TEST:springfield-mo"),
            "Springfield, IL (near Chicago) should outrank the larger, distant Springfield, MO.");
    }

    [Fact]
    public async Task No_home_city_falls_back_to_population_without_error()
    {
        // Feature 032 (FR-004): a user with no home city gets ranked results with no distance influence
        // and no error — the more populous Springfield (Missouri) leads.
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var resp = await client.GetAsync("/api/v1/cities/search?q=springfield");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var ids = await OrderedExternalIdsAsync(client, "springfield");
        Assert.True(ids.IndexOf("TEST:springfield-mo") < ids.IndexOf("TEST:springfield-il"));
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

    /// <summary>Runs a search and returns the option external ids in the order the API returned them.</summary>
    private static async Task<List<string>> OrderedExternalIdsAsync(HttpClient client, string q)
    {
        var resp = await client.GetAsync($"/api/v1/cities/search?q={q}");
        resp.EnsureSuccessStatusCode();
        var items = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return items.EnumerateArray().Select(c => c.GetProperty("externalId").GetString()!).ToList();
    }
}
