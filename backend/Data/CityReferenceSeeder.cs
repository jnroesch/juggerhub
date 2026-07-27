using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace JuggerHub.Data;

/// <summary>
/// Loads the bundled GeoNames <c>cities500</c> snapshot into <c>CityReferences</c> on startup, in
/// every environment (feature 030, research R8). Idempotent — only runs when the table is empty —
/// and uses a single binary <c>COPY</c> so ~235k rows import in a second or two. The gzipped seed
/// is copied next to the app (<c>Data/Seed/cities500.seed.tsv.gz</c>); regenerate it with
/// <c>Data/Seed/regenerate-cities500.mjs</c>.
/// </summary>
public static class CityReferenceSeeder
{
    private const string CopyCommand =
        "COPY \"CityReferences\" (\"ExternalId\",\"Name\",\"AsciiName\",\"AlternateNames\"," +
        "\"CountryCode\",\"CountryName\",\"Region\",\"Latitude\",\"Longitude\",\"Population\") FROM STDIN (FORMAT BINARY)";

    public static async Task SeedAsync(AppDbContext db, string baseDirectory, ILogger logger, CancellationToken ct = default)
    {
        if (await db.CityReferences.AnyAsync(ct))
        {
            return; // already seeded
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

        try
        {
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
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
