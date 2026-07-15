using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventMarketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ViaMarket",
                table: "PartyMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecruiting",
                table: "Parties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int[]>(
                name: "PositionsNeeded",
                table: "Parties",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<string>(
                name: "RecruitBlurb",
                table: "Parties",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpotsAdvertised",
                table: "Parties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MarketRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Positions = table.Column<int[]>(type: "integer[]", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketRequests_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MercenaryListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Positions = table.Column<int[]>(type: "integer[]", nullable: false),
                    Pitch = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MercenaryListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MercenaryListings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MercenaryListings_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRequests_PartyId_Status",
                table: "MarketRequests",
                columns: new[] { "PartyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRequests_PartyId_UserId",
                table: "MarketRequests",
                columns: new[] { "PartyId", "UserId" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MarketRequests_UserId_Status",
                table: "MarketRequests",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MercenaryListings_EventId",
                table: "MercenaryListings",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_MercenaryListings_UserId_EventId",
                table: "MercenaryListings",
                columns: new[] { "UserId", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketRequests");

            migrationBuilder.DropTable(
                name: "MercenaryListings");

            migrationBuilder.DropColumn(
                name: "ViaMarket",
                table: "PartyMembers");

            migrationBuilder.DropColumn(
                name: "IsRecruiting",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "PositionsNeeded",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "RecruitBlurb",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "SpotsAdvertised",
                table: "Parties");
        }
    }
}
