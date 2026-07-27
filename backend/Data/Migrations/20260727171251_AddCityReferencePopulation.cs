using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <summary>
    /// Adds <c>CityReferences.Population</c> (feature 032, relevance ranking). Existing rows get the
    /// <c>0</c> default; real values arrive only from the regenerated 10-column cities500 seed.
    /// DEPLOY NOTE: because <c>CityReferenceSeeder</c> only runs on an empty table, a one-time reseed
    /// (truncate <c>CityReferences</c> so the seeder reloads the regenerated bundle) is required in each
    /// environment to backfill population — reference data only, never user-authored.
    /// </summary>
    public partial class AddCityReferencePopulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Population",
                table: "CityReferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Population",
                table: "CityReferences");
        }
    }
}
