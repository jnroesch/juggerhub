using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "PlayerProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "PlayerProfiles");
        }
    }
}
