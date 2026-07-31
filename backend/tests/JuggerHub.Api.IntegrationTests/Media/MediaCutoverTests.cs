using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Media;

/// <summary>
/// End-state checks for the cutover to object storage (feature 035 / #97, US3).
/// </summary>
/// <remarks>
/// Existing media was discarded rather than migrated — an owner decision that knowingly waives the
/// "Existing avatars migrated" criterion on GH #97. What still has to hold is <b>end-state
/// consistency</b>: exactly one storage mechanism, with no leftovers from the other. These assert
/// the schema half of that; the behavioural half (a member whose picture was dropped sees the
/// ordinary placeholder, then uploads again successfully) is covered in
/// <see cref="MediaPrivacyTests"/> and the profile suite.
/// </remarks>
public sealed class MediaCutoverTests : IClassFixture<JuggerHubApiFactory>
{
    private readonly JuggerHubApiFactory _factory;

    public MediaCutoverTests(JuggerHubApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("ProfileAvatars")]
    [InlineData("BadgeIcons")]
    [InlineData("AchievementIcons")]
    public async Task Media_byte_column_is_gone_from_the_database(string table)
    {
        // SC-002: zero image bytes remain in the primary database. Asserted against the live schema
        // rather than the model, so a migration that failed to drop the column is caught here.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await ColumnsAsync(db, table);

        Assert.DoesNotContain("Bytes", columns);
        Assert.Contains("ObjectKey", columns);
        Assert.Contains("SizeBytes", columns);
    }

    [Fact]
    public async Task No_descriptor_points_at_a_missing_object()
    {
        // FR-019. A surviving row with an empty ObjectKey is the "record without object" state: it
        // would render as a broken image rather than the placeholder members already understand.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await db.ProfileAvatars.IgnoreQueryFilters().AnyAsync(a => a.ObjectKey == string.Empty));
        Assert.False(await db.BadgeIcons.AnyAsync(i => i.ObjectKey == string.Empty));
        Assert.False(await db.AchievementIcons.AnyAsync(i => i.ObjectKey == string.Empty));
    }

    [Fact]
    public async Task Migrations_are_fully_applied_and_re_running_is_a_no_op()
    {
        // SC-007: re-applying against an already-cut-over database changes nothing and errors on
        // nothing. The app migrates on startup, so reaching this point already proves one apply;
        // this asserts there is no pending work left behind and that a second call is inert.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(
            await db.Database.GetAppliedMigrationsAsync(),
            id => id.EndsWith("MediaObjectStorage", StringComparison.Ordinal));

        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private static async Task<List<string>> ColumnsAsync(AppDbContext db, string table)
    {
        var columns = new List<string>();

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT column_name FROM information_schema.columns WHERE table_name = @table";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "table";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        await db.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }
}
