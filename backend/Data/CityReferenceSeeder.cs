using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace JuggerHub.Data;

/// <summary>
/// Loads the bundled GeoNames <c>cities500</c> snapshot into <c>CityReferences</c> on startup, in
/// every environment (feature 030, research R8). Idempotent — only runs when the table is empty —
/// and uses a single binary <c>COPY</c> so ~225k rows import in a second or two. The gzipped seed
/// is copied next to the app (<c>Data/Seed/cities500.seed.tsv.gz</c>); regenerate it with
/// <c>Data/Seed/regenerate-cities500.mjs</c>, which drops city districts and dead places by GeoNames
/// feature code — keep that filter, or "Hamburg" starts offering Hamburg-Nord and Hamburg-Altstadt
/// alongside Hamburg again.
/// <para>
/// Because it skips a non-empty table, shipping a regenerated bundle does NOT reload an environment
/// that already has one: that needs a migration emptying the table (see <c>ReseedCityReferences</c>).
/// </para>
/// </summary>
public static class CityReferenceSeeder
{
    private const string CopyCommand =
        "COPY \"CityReferences\" (\"ExternalId\",\"Name\",\"AsciiName\",\"AlternateNames\"," +
        "\"CountryCode\",\"CountryName\",\"Region\",\"Latitude\",\"Longitude\",\"Population\") FROM STDIN (FORMAT BINARY)";

    /// <summary>
    /// Arbitrary but fixed key for the Postgres session advisory lock that serialises the import
    /// across replicas (digits of feature 030 + the cities500 bundle). Only this seeder uses it.
    /// </summary>
    private const long AdvisoryLockKey = 30_500L;

    public static async Task SeedAsync(AppDbContext db, string baseDirectory, ILogger logger, CancellationToken ct = default)
    {
        if (await db.CityReferences.AnyAsync(ct))
        {
            return; // already seeded — the common case, and it never touches the lock
        }

        var path = Path.Combine(baseDirectory, "Data", "Seed", "cities500.seed.tsv.gz");
        if (!File.Exists(path))
        {
            // Non-fatal: the app still runs; city search simply returns nothing until the seed is present.
            logger.LogError("City reference seed file not found at {Path}; the city picker will be empty.", path);
            return;
        }

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(ct);
        }

        var locked = false;
        try
        {
            // The deployment runs multiple replicas, all of which start this seeder. Two concurrent
            // COPYs into an empty table both insert every row, and the loser dies on the ExternalId
            // primary key — crash-looping the pod until the winner commits. Serialise on a session
            // advisory lock and re-check emptiness once held: the winner imports, everyone else
            // observes a populated table and returns. Matters on any reseed of a LIVE environment
            // (the ReseedCityReferences migration), not just on a virgin database.
            await using (var acquire = connection.CreateCommand())
            {
                acquire.CommandText = $"SELECT pg_advisory_lock({AdvisoryLockKey})";
                await acquire.ExecuteNonQueryAsync(ct);
                locked = true;
            }

            await using (var recheck = connection.CreateCommand())
            {
                recheck.CommandText = "SELECT EXISTS (SELECT 1 FROM \"CityReferences\")";
                if (await recheck.ExecuteScalarAsync(ct) is true)
                {
                    logger.LogInformation("City reference seed skipped: another replica loaded it first.");
                    return;
                }
            }

            await using var importer = await connection.BeginBinaryImportAsync(CopyCommand, ct);
            await using var file = File.OpenRead(path);
            await using var gz = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(gz);

            var count = 0;
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                var c = line.Split('\t');
                if (c.Length < 9)
                {
                    continue;
                }

                await importer.StartRowAsync(ct);
                await importer.WriteAsync(c[0], NpgsqlDbType.Varchar, ct);
                await importer.WriteAsync(c[1], NpgsqlDbType.Varchar, ct);
                await importer.WriteAsync(c[2], NpgsqlDbType.Varchar, ct);
                await importer.WriteAsync(c[3], NpgsqlDbType.Varchar, ct);
                await importer.WriteAsync(c[4], NpgsqlDbType.Varchar, ct);
                await importer.WriteAsync(c[5], NpgsqlDbType.Varchar, ct);
                await importer.WriteAsync(c[6], NpgsqlDbType.Varchar, ct);
                await importer.WriteAsync(double.Parse(c[7], CultureInfo.InvariantCulture), NpgsqlDbType.Double, ct);
                await importer.WriteAsync(double.Parse(c[8], CultureInfo.InvariantCulture), NpgsqlDbType.Double, ct);
                // Population is the 10th column (feature 032). Tolerate the pre-032 9-column bundle by
                // defaulting to 0 (unknown → sorts last) so an older seed still imports cleanly.
                var population = c.Length > 9 && int.TryParse(c[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 0;
                await importer.WriteAsync(population, NpgsqlDbType.Integer, ct);
                count++;
            }

            await importer.CompleteAsync(ct);
            logger.LogInformation("Seeded {Count} city reference rows (cities500).", count);
        }
        finally
        {
            // Session locks die with the connection, so this only matters when EF handed us an
            // already-open connection we must leave usable. CancellationToken.None: releasing must
            // not be skipped because the caller cancelled.
            if (locked)
            {
                await using var release = connection.CreateCommand();
                release.CommandText = $"SELECT pg_advisory_unlock({AdvisoryLockKey})";
                await release.ExecuteNonQueryAsync(CancellationToken.None);
            }

            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
