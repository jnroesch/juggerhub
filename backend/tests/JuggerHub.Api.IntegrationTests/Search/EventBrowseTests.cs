using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

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

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);

        var upcoming = await IdsAsync(viewer, $"/api/v1/events?city={city}&take=100");
        Assert.Contains(future.ToString(), upcoming);
        Assert.DoesNotContain(past.ToString(), upcoming);
        Assert.DoesNotContain(cancelled.ToString(), upcoming);

        // hidePast=false reveals past, but cancelled stays hidden either way.
        var withPast = await IdsAsync(viewer, $"/api/v1/events?city={city}&hidePast=false&take=100");
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

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);

        // A range covering only the soon event.
        var from = DateOnly.FromDateTime(now.AddDays(1));
        var to = DateOnly.FromDateTime(now.AddDays(10));
        var ranged = await IdsAsync(viewer, $"/api/v1/events?city={city}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&take=100");
        Assert.Contains(soon.ToString(), ranged);
        Assert.DoesNotContain(later.ToString(), ranged);

        // Type filter.
        var workshops = await IdsAsync(viewer, $"/api/v1/events?city={city}&type=Workshop&take=100");
        Assert.Contains(later.ToString(), workshops);
        Assert.DoesNotContain(soon.ToString(), workshops);
    }

    [Fact]
    public async Task Name_search_is_accent_insensitive()
    {
        var city = "Evt" + Rnd();
        var now = DateTime.UtcNow;
        var id = await SearchTestSupport.SeedEventAsync(_factory, "Süd Turnier " + Rnd(), now.AddDays(5), now.AddDays(6), city: city);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var byUnaccented = await IdsAsync(viewer, $"/api/v1/events?city={city}&q=sud&take=100");
        Assert.Contains(id.ToString(), byUnaccented);
    }

    [Fact]
    public async Task Results_are_ordered_soonest_first()
    {
        var city = "Evt" + Rnd();
        var now = DateTime.UtcNow;
        var later = await SearchTestSupport.SeedEventAsync(_factory, "B Later " + Rnd(), now.AddDays(20), now.AddDays(21), city: city);
        var sooner = await SearchTestSupport.SeedEventAsync(_factory, "A Sooner " + Rnd(), now.AddDays(5), now.AddDays(6), city: city);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var ids = await IdsAsync(viewer, $"/api/v1/events?city={city}&take=100");

        Assert.True(ids.IndexOf(sooner.ToString()) < ids.IndexOf(later.ToString()));
    }

    [Fact]
    public async Task Browse_is_anonymous_and_paginates()
    {
        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var resp = await viewer.GetAsync("/api/v1/events?take=5");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var page = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, page.GetProperty("take").GetInt32());
        Assert.True(page.GetProperty("items").GetArrayLength() <= 5);
    }

    // --- Feature 030: proximity + country filter -----------------------------

    [Fact]
    public async Task Proximity_sort_orders_events_nearest_to_the_players_home_city_first()
    {
        var now = DateTime.UtcNow;
        var token = "EvtProx" + Rnd();
        var berlin = await SearchTestSupport.SeedEventAsync(_factory, $"{token} Berlin", now.AddDays(5), now.AddDays(6), city: "Berlin");
        var munich = await SearchTestSupport.SeedEventAsync(_factory, $"{token} Munich", now.AddDays(5), now.AddDays(6), city: "München");

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        (await viewer.PutAsJsonAsync("/api/v1/profiles/me/home-city",
            new { cityExternalId = "TEST:berlin", name = "Berlin" })).EnsureSuccessStatusCode();

        var ids = await IdsAsync(viewer, $"/api/v1/events?q={Uri.EscapeDataString(token)}&sort=Proximity&take=100");

        var iBerlin = ids.IndexOf(berlin.ToString());
        var iMunich = ids.IndexOf(munich.ToString());
        Assert.True(iBerlin >= 0 && iMunich >= 0, "both located events are present");
        Assert.True(iBerlin < iMunich, "the home-city (Berlin) event ranks ahead of the Munich event");
    }

    [Fact]
    public async Task Proximity_sort_excludes_virtual_events()
    {
        var now = DateTime.UtcNow;
        var token = "EvtVirt" + Rnd();
        var inPerson = await SearchTestSupport.SeedEventAsync(_factory, $"{token} InPerson", now.AddDays(5), now.AddDays(6), city: "Berlin");
        var virtual_ = await SearchTestSupport.SeedEventAsync(_factory, $"{token} Online", now.AddDays(5), now.AddDays(6), city: null);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        (await viewer.PutAsJsonAsync("/api/v1/profiles/me/home-city",
            new { cityExternalId = "TEST:berlin", name = "Berlin" })).EnsureSuccessStatusCode();

        var proximity = await IdsAsync(viewer, $"/api/v1/events?q={Uri.EscapeDataString(token)}&sort=Proximity&take=100");
        Assert.Contains(inPerson.ToString(), proximity);
        Assert.DoesNotContain(virtual_.ToString(), proximity); // no city → not in the proximity view (FR-016)

        // …but it reappears under the default date sort.
        var byDate = await IdsAsync(viewer, $"/api/v1/events?q={Uri.EscapeDataString(token)}&take=100");
        Assert.Contains(virtual_.ToString(), byDate);
    }

    [Fact]
    public async Task Proximity_total_excludes_events_whose_city_has_no_cached_distance()
    {
        // Regression for #146: the proximity total must apply the SAME CityDistances exclusion the
        // inner join applies. An event whose city has no cached distance row from the caller's home
        // city is dropped from the page, so it must drop from totalCount too — otherwise
        // BrowseList.hasMore (items.length < total) leaves "load more" chasing a count it can never
        // reach. Uses unique cities and deletes one distance row to model a broken invariant
        // (partial backfill, a city inserted bypassing CityService, a failed mid-write).
        var now = DateTime.UtcNow;
        var token = "EvtDist" + Rnd();
        var homeCity = "DistHome" + Rnd();
        var orphanCity = "DistOrphan" + Rnd();

        var near = await SearchTestSupport.SeedEventAsync(_factory, $"{token} Near", now.AddDays(5), now.AddDays(6), city: homeCity);
        var orphan = await SearchTestSupport.SeedEventAsync(_factory, $"{token} Orphan", now.AddDays(5), now.AddDays(6), city: orphanCity);

        var (viewer, viewerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.SetHomeCityAsync(_factory, viewerId, homeCity);

        // Break the invariant: remove the home -> orphan cached distance so the join drops the
        // orphan-city event.
        await SearchTestSupport.WithDbAsync(_factory, async db =>
        {
            var homeId = await db.Cities.Where(c => c.ExternalId == "TEST:" + homeCity.ToLowerInvariant())
                .Select(c => c.Id).FirstAsync();
            var orphanId = await db.Cities.Where(c => c.ExternalId == "TEST:" + orphanCity.ToLowerInvariant())
                .Select(c => c.Id).FirstAsync();
            await db.CityDistances
                .Where(d => d.FromCityId == homeId && d.ToCityId == orphanId)
                .ExecuteDeleteAsync();
        });

        var page = await PageAsync(viewer, $"/api/v1/events?q={Uri.EscapeDataString(token)}&sort=Proximity&take=100");
        var ids = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetString()!).ToList();

        // The orphan-city event drops out of the items (the join has no distance row)...
        Assert.Contains(near.ToString(), ids);
        Assert.DoesNotContain(orphan.ToString(), ids);

        // ...and out of the total, so "load more" cannot chase a count it can never reach.
        Assert.Equal(ids.Count, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Country_filter_matches_the_events_country_and_excludes_others()
    {
        var now = DateTime.UtcNow;
        var token = "EvtCtry" + Rnd();
        var id = await SearchTestSupport.SeedEventAsync(_factory, $"{token} Cup", now.AddDays(5), now.AddDays(6), city: "Berlin");

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var matched = await IdsAsync(viewer, $"/api/v1/events?q={Uri.EscapeDataString(token)}&country=DE&take=100");
        var other = await IdsAsync(viewer, $"/api/v1/events?q={Uri.EscapeDataString(token)}&country=France&take=100");

        Assert.Contains(id.ToString(), matched);
        Assert.DoesNotContain(id.ToString(), other);
    }

    private static string Rnd() => Guid.NewGuid().ToString("N")[..6];

    private static async Task<JsonElement> PageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode,
            $"GET {url} failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<List<string>> IdsAsync(HttpClient client, string url)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(url);
        return page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetString()!)
            .ToList();
    }
}
