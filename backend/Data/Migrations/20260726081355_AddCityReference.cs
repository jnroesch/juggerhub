using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCityReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CityReferences",
                columns: table => new
                {
                    ExternalId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AsciiName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AlternateNames = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CountryName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Region = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityReferences", x => x.ExternalId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CityReferences_AsciiName",
                table: "CityReferences",
                column: "AsciiName");

            migrationBuilder.CreateIndex(
                name: "IX_CityReferences_CountryCode",
                table: "CityReferences",
                column: "CountryCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CityReferences");
        }
    }
}
