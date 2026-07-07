using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// Event browse/search (007, US2): hide-past default, cancelled events always excluded, the
/// date-range + type + city filters, name search, soonest-first ordering, anonymous access
/// and pagination. Exercises the real API + Postgres container.
/// </summary>
[Collection("Search")]
public sealed class EventBrowseTests
{
    private readonly JuggerHubApiFactory _factory;

    public EventBrowseTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Hides_past_by_default_and_always_excludes_cancelled()
    {
        var city = "Evt" + Rnd();
        var now = DateTime.UtcNow;
        var past = await SearchTestSupport.SeedEventAsync(_factory, "Past " + Rnd(), now.AddDays(-10), now.AddDays(-9), city: city);
        var future = await SearchTestSupport.SeedEventAsync(_factory, "Future " + Rnd(), now.AddDays(9), now.AddDays(10), city: city);
        var cancelled = await SearchTestSupport.SeedEventAsync(_factory, "Cancelled " + Rnd(), now.AddDays(11), now.AddDays(12),
            city: city, status: EventStatus.Cancelled);

        var anon = _factory.CreateClient();

        var upcoming = await IdsAsync(anon, $"/api/v1/events?city={city}&take=100");
        Assert.Contains(future.ToString(), upcoming);
        Assert.DoesNotContain(past.ToString(), upcoming);
        Assert.DoesNotContain(cancelled.ToString(), upcoming);

        // hidePast=false reveals past, but cancelled stays hidden either way.
        var withPast = await IdsAsync(anon, $"/api/v1/events?city={city}&hidePast=false&take=100");
        Assert.Contains(past.ToString(), withPast);
        Assert.Contains(future.ToString(), withPast);
        Assert.DoesNotContain(cancelled.ToString(), withPast);
    }

    [Fact]
    public async Task Date_range_and_type_filters_narrow_results()
    {
        var city = "Evt" + Rnd();
        var now = DateTime.UtcNow;
        var soon = await SearchTestSupport.SeedEventAsync(_factory, "Soon " + Rnd(), now.AddDays(3), now.AddDays(4),
            type: EventType.Tournament, city: city);
        var later = await SearchTestSupport.SeedEventAsync(_factory, "Later " + Rnd(), now.AddDays(40), now.AddDays(41),
            type: EventType.Workshop, city: city);

        var anon = _factory.CreateClient();

        // A range covering only the soon event.
        var from = DateOnly.FromDateTime(now.AddDays(1));
        var to = DateOnly.FromDateTime(now.AddDays(10));
        var ranged = await IdsAsync(anon, $"/api/v1/events?city={city}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&take=100");
        Assert.Contains(soon.ToString(), ranged);
        Assert.DoesNotContain(later.ToString(), ranged);

        // Type filter.
        var workshops = await IdsAsync(anon, $"/api/v1/events?city={city}&type=Workshop&take=100");
        Assert.Contains(later.ToString(), workshops);
        Assert.DoesNotContain(soon.ToString(), workshops);
    }

    [Fact]
    public async Task Name_search_is_accent_insensitive()
    {
        var city = "Evt" + Rnd();
        var now = DateTime.UtcNow;
        var id = await SearchTestSupport.SeedEventAsync(_factory, "Süd Turnier " + Rnd(), now.AddDays(5), now.AddDays(6), city: city);

        var anon = _factory.CreateClient();
        var byUnaccented = await IdsAsync(anon, $"/api/v1/events?city={city}&q=sud&take=100");
        Assert.Contains(id.ToString(), byUnaccented);
    }

    [Fact]
    public async Task Results_are_ordered_soonest_first()
    {
        var city = "Evt" + Rnd();
        var now = DateTime.UtcNow;
        var later = await SearchTestSupport.SeedEventAsync(_factory, "B Later " + Rnd(), now.AddDays(20), now.AddDays(21), city: city);
        var sooner = await SearchTestSupport.SeedEventAsync(_factory, "A Sooner " + Rnd(), now.AddDays(5), now.AddDays(6), city: city);

        var anon = _factory.CreateClient();
        var ids = await IdsAsync(anon, $"/api/v1/events?city={city}&take=100");

        Assert.True(ids.IndexOf(sooner.ToString()) < ids.IndexOf(later.ToString()));
    }

    [Fact]
    public async Task Browse_is_anonymous_and_paginates()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/v1/events?take=5");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var page = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, page.GetProperty("take").GetInt32());
        Assert.True(page.GetProperty("items").GetArrayLength() <= 5);
    }

    private static string Rnd() => Guid.NewGuid().ToString("N")[..6];

    private static async Task<List<string>> IdsAsync(HttpClient client, string url)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(url);
        return page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetString()!)
            .ToList();
    }
}
