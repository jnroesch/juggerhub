using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// CityService behavior (feature 030) exercised through the real API + DB: de-duplication by
/// provider id, the city-to-city distance backfill (self-row + both directions), and rejection of
/// an unresolvable selection. Uses per-test synthetic <c>TEST:{token}</c> ids (the TestGeocoder
/// resolves any of them) so assertions are isolated in the shared database.
/// </summary>
[Collection("Search")]
public sealed class CityServiceTests
{
    private readonly JuggerHubApiFactory _factory;

    public CityServiceTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Selecting_the_same_city_twice_creates_one_row_and_reuses_it()
    {
        var ext = "TEST:dedupe" + Guid.NewGuid().ToString("N")[..8];

        var (a, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (b, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        (await a.PutAsJsonAsync("/api/v1/profiles/me/home-city", new { cityExternalId = ext, name = "Dedupe City" }))
            .EnsureSuccessStatusCode();
        (await b.PutAsJsonAsync("/api/v1/profiles/me/home-city", new { cityExternalId = ext, name = "Dedupe City" }))
            .EnsureSuccessStatusCode();

        var count = await SearchTestSupport.WithDbAsync(_factory, db =>
            db.Cities.CountAsync(c => c.ExternalId == ext));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Creating_a_city_backfills_self_row_and_both_directions()
    {
        var extX = "TEST:distx" + Guid.NewGuid().ToString("N")[..8];
        var extY = "TEST:disty" + Guid.NewGuid().ToString("N")[..8];

        var (a, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (b, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        (await a.PutAsJsonAsync("/api/v1/profiles/me/home-city", new { cityExternalId = extX, name = "X City" }))
            .EnsureSuccessStatusCode();
        (await b.PutAsJsonAsync("/api/v1/profiles/me/home-city", new { cityExternalId = extY, name = "Y City" }))
            .EnsureSuccessStatusCode();

        var (selfZero, xToY, yToX) = await SearchTestSupport.WithDbAsync(_factory, async db =>
        {
            var x = await db.Cities.FirstAsync(c => c.ExternalId == extX);
            var y = await db.Cities.FirstAsync(c => c.ExternalId == extY);
            var self = await db.CityDistances.FirstOrDefaultAsync(d => d.FromCityId == x.Id && d.ToCityId == x.Id);
            var xy = await db.CityDistances.FirstOrDefaultAsync(d => d.FromCityId == x.Id && d.ToCityId == y.Id);
            var yx = await db.CityDistances.FirstOrDefaultAsync(d => d.FromCityId == y.Id && d.ToCityId == x.Id);
            return (self, xy, yx);
        });

        Assert.NotNull(selfZero);
        Assert.Equal(0, selfZero!.DistanceKm);
        Assert.NotNull(xToY);
        Assert.NotNull(yToX);
        Assert.Equal(xToY!.DistanceKm, yToX!.DistanceKm, precision: 6); // symmetric
    }

    [Fact]
    public async Task An_unresolvable_city_selection_is_rejected_with_422()
    {
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        // The TestGeocoder resolves only "TEST:*" ids; anything else comes back unresolved.
        var resp = await client.PutAsJsonAsync("/api/v1/profiles/me/home-city",
            new { cityExternalId = "NOPE:404", name = "Nowhere" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }
}
