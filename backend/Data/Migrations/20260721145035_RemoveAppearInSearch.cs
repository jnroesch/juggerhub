using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAppearInSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerProfiles_AppearInSearch",
                table: "PlayerProfiles");

            migrationBuilder.DropColumn(
                name: "AppearInSearch",
                table: "PlayerProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
