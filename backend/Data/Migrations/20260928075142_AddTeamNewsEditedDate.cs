using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamNewsEditedDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditedDate",
                table: "TeamNewsPosts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditedDate",
                table: "TeamNewsPosts");
        }
    }
}
