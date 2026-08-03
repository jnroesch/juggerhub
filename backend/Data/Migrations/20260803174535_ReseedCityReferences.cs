using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <summary>
    /// Empties <c>CityReferences</c> so the regenerated cities500 bundle — the one that no longer
    /// carries city districts (GeoNames <c>PPLX</c>: Hamburg-Nord, Hamburg-Altstadt, …) — is loaded
    /// in every environment.
    /// <para>
    /// No schema change: this migration exists ONLY to trigger the reseed.
    /// <c>CityReferenceSeeder</c> runs at startup but returns early on a non-empty table, so shipping
    /// a new bundle is otherwise a no-op wherever the old one is already loaded (feature 032 hit the
    /// same wall and resolved it with a manual truncate — this automates it). Ordering holds because
    /// migrations run to completion before the seeder is invoked in <c>Program</c>.
    /// </para>
    /// <para>
    /// Safe to delete outright: <c>CityReferences</c> is bundled reference data, never user-authored,
    /// and nothing has a foreign key to it — a selected city is COPIED into <c>Cities</c>, which is
    /// left untouched here. Consequence: a city already selected from a now-dropped district row keeps
    /// resolving from <c>Cities</c> and stays as it is until re-picked.
    /// </para>
    /// </summary>
    public partial class ReseedCityReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"CityReferences\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo — and nothing lost. Rolling back leaves the table empty; the seeder
            // repopulates it from whichever bundle the running build carries.
        }
    }
}
