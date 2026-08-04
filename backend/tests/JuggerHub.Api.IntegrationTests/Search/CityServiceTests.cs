using System.Net;
using System.Net.Http.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Geocoding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// Losing the create race must cost the loser its own insert and NOTHING else. The recovery used
    /// to be <c>ChangeTracker.Clear()</c>, which emptied the whole request-scoped context: every edit
    /// path loads its entity, resolves the picked city, then assigns — so the caller's save wrote no
    /// row and still reported success. Reachable from the event edit form since GH #136 made an
    /// event's city changeable (before that it only ever re-sent a city that already existed).
    /// </summary>
    [Fact]
    public async Task Losing_the_city_create_race_keeps_the_callers_other_changes()
    {
        // A reference row of our own, so no other test can have materialised this city already.
        var externalId = "TEST:race-" + Guid.NewGuid().ToString("N")[..8];
        await SearchTestSupport.WithDbAsync(_factory, async db =>
        {
            db.CityReferences.Add(new CityReference
            {
                ExternalId = externalId,
                Name = "Racetown",
                AsciiName = "Racetown",
                AlternateNames = "",
                CountryCode = "DE",
                CountryName = "Germany",
                Region = "Nowhere",
                Latitude = 51.0,
                Longitude = 7.0,
                Population = 1_000,
            });
            await db.SaveChangesAsync();
        });

        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Race Crew", city: null);

        // The loser: one scope = one DbContext, exactly like a request. It loads and edits a team,
        // then resolves a city — the order every edit path uses.
        using var loserScope = _factory.Services.CreateScope();
        var loserDb = loserScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cities = loserScope.ServiceProvider.GetRequiredService<ICityService>();
        var team = await loserDb.Teams.FirstAsync(t => t.Id == teamId);
        team.Name = "Renamed mid-race";

        // The winner inserts the same city inside an UNCOMMITTED transaction: the loser's existence
        // check can't see the row, so it tries its own insert and blocks on the unique index until
        // the winner commits — which is the race, made deterministic.
        using var winnerScope = _factory.Services.CreateScope();
        var winnerDb = winnerScope.ServiceProvider.GetRequiredService<AppDbContext>();
        City winner = null!;

        // Wrapped because the provider is configured with EnableRetryOnFailure, which refuses
        // user-initiated transactions outside an execution strategy. Nothing here is transient, so
        // the delegate runs once.
        await winnerDb.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await winnerDb.Database.BeginTransactionAsync();
            winnerDb.Cities.Add(new City
            {
                ExternalId = externalId,
                Name = "Racetown",
                CountryName = "Germany",
                CountryCode = "DE",
                Region = "Nowhere",
                Latitude = 51.0,
                Longitude = 7.0,
            });
            await winnerDb.SaveChangesAsync();

            var losing = cities.ResolveAndUpsertAsync(externalId, "Racetown");
            await Task.Delay(TimeSpan.FromSeconds(1));

            // If this ever fails the race never happened (the loser saw the row and returned it),
            // and everything below would pass without exercising the recovery path at all.
            Assert.False(losing.IsCompleted, "The loser should be blocked on the uncommitted insert.");

            await tx.CommitAsync();
            winner = await losing;
        });

        // It returns the winner's row rather than its own discarded insert…
        Assert.Equal(externalId, winner.ExternalId);
        var winnerId = await SearchTestSupport.WithDbAsync(_factory, db =>
            db.Cities.Where(c => c.ExternalId == externalId).Select(c => c.Id).FirstAsync());
        Assert.Equal(winnerId, winner.Id);

        // …and the edit the caller was in the middle of still reaches the database.
        await loserDb.SaveChangesAsync();
        var storedName = await SearchTestSupport.WithDbAsync(_factory, db =>
            db.Teams.Where(t => t.Id == teamId).Select(t => t.Name).FirstAsync());
        Assert.Equal("Renamed mid-race", storedName);
    }
}
