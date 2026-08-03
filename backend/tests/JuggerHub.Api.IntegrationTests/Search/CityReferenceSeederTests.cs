using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// Exercises the REAL startup seeding path against a real database — the full cities500 bundle, the
/// genuine binary <c>COPY</c>, and the schema as the migrations leave it. Everything else in this
/// suite runs with <c>Seeding:CityReferences=false</c> and a tiny <see cref="TestReferenceCities"/>
/// fixture, so without this the code that populates every environment (and the reseed the
/// <c>ReseedCityReferences</c> migration triggers) would ship untested.
/// <para>
/// Owns its own container rather than joining a collection: it deliberately seeds ~225k rows, which
/// would leave every other test in that collection searching a different dataset.
/// </para>
/// </summary>
public sealed class CityReferenceSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task Concurrent_replicas_seed_the_bundle_exactly_once_with_one_option_per_city()
    {
        await using (var migrator = NewContext())
        {
            await migrator.Database.MigrateAsync();
        }

        // Two pods starting together, as a rolling deploy does. Without the advisory lock both
        // observe an empty table and race into the COPY; the loser dies on the ExternalId primary
        // key and crash-loops.
        await using var first = NewContext();
        await using var second = NewContext();
        await Task.WhenAll(
            CityReferenceSeeder.SeedAsync(first, AppContext.BaseDirectory, NullLogger.Instance),
            CityReferenceSeeder.SeedAsync(second, AppContext.BaseDirectory, NullLogger.Instance));

        await using var db = NewContext();
        var total = await db.CityReferences.CountAsync();
        Assert.True(total > 100_000, $"Only {total} rows seeded — the bundle did not load.");

        // The point of the whole exercise: Hamburg is one option, not Hamburg plus its boroughs.
        var hamburg = await db.CityReferences
            .Where(r => r.CountryCode == "DE" && r.Name.StartsWith("Hamburg"))
            .Select(r => r.Name)
            .ToListAsync();
        Assert.Equal(["Hamburg"], hamburg);
    }

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .Options);
}
