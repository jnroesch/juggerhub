using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// CityService behavior (feature 030, R8) exercised through the real API + DB: de-duplication by
/// provider id, the city-to-city distance backfill (self-row + both directions), and rejection of a
/// selection whose id isn't in the bundled reference. Selects by fixture <c>TEST:*</c> ids seeded
/// into <c>CityReference</c> (<see cref="TestReferenceCities"/>).
/// </summary>
[Collection("Search")]
public sealed class CityServiceTests
{
    private readonly JuggerHubApiFactory _factory;

    public CityServiceTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Selecting_the_same_city_twice_creates_one_row_and_reuses_it()
    {
        var (a, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (b, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        (await a.PutAsJsonAsync("/api/v1/profiles/me/home-city", new { cityExternalId = "TEST:köln", name = "Köln" }))
            .EnsureSuccessStatusCode();
        (await b.PutAsJsonAsync("/api/v1/profiles/me/home-city", new { cityExternalId = "TEST:köln", name = "Köln" }))
            .EnsureSuccessStatusCode();

        var count = await SearchTestSupport.WithDbAsync(_factory, db =>
            db.Cities.CountAsync(c => c.ExternalId == "TEST:köln"));
        Assert.Equal(1, count); // resolving the same reference id twice reuses the one City (unique index)
    }

    [Fact]
    public async Task Selecting_a_city_backfills_self_row_and_both_directions()
    {
        var (a, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (b, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        (await a.PutAsJsonAsync("/api/v1/profiles/me/home-city", new { cityExternalId = "TEST:berlin", name = "Berlin" }))
            .EnsureSuccessStatusCode();
        (await b.PutAsJsonAsync("/api/v1/profiles/me/home-city", new { cityExternalId = "TEST:münchen", name = "München" }))
            .EnsureSuccessStatusCode();

        var (selfZero, xToY, yToX) = await SearchTestSupport.WithDbAsync(_factory, async db =>
        {
            var x = await db.Cities.FirstAsync(c => c.ExternalId == "TEST:berlin");
            var y = await db.Cities.FirstAsync(c => c.ExternalId == "TEST:münchen");
            var self = await db.CityDistances.FirstOrDefaultAsync(d => d.FromCityId == x.Id && d.ToCityId == x.Id);
            var xy = await db.CityDistances.FirstOrDefaultAsync(d => d.FromCityId == x.Id && d.ToCityId == y.Id);
            var yx = await db.CityDistances.FirstOrDefaultAsync(d => d.FromCityId == y.Id && d.ToCityId == x.Id);
            return (self, xy, yx);
        });

        Assert.NotNull(selfZero);
        Assert.Equal(0, selfZero!.DistanceKm);
        Assert.NotNull(xToY);
        Assert.NotNull(yToX);
        Assert.Equal(xToY!.DistanceKm, yToX!.DistanceKm, precision: 6);   // symmetric
        Assert.InRange(xToY.DistanceKm, 450, 550);                        // Berlin↔München ≈ 500 km
    }

    [Fact]
    public async Task A_city_id_absent_from_the_reference_is_rejected_with_422()
    {
        var (client, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        var resp = await client.PutAsJsonAsync("/api/v1/profiles/me/home-city",
            new { cityExternalId = "geonames:999999999", name = "Nowhere" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }
}
