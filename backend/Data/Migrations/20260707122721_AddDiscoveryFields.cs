using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscoveryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Accent-insensitive search (feature 007): AppDbContext.Unaccent maps to this
            // extension's unaccent(text) function, used in browse ILIKE predicates.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");

            migrationBuilder.AddColumn<bool>(
                name: "BeginnersWelcome",
                table: "Teams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AppearInSearch",
                table: "PlayerProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_AppearInSearch",
                table: "PlayerProfiles",
                column: "AppearInSearch",
                filter: "\"AppearInSearch\"");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Status",
                table: "Events",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerProfiles_AppearInSearch",
                table: "PlayerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Events_Status",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "BeginnersWelcome",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AppearInSearch",
                table: "PlayerProfiles");
        }
    }
}
