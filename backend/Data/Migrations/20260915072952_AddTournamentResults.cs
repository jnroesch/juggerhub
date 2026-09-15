using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedSinceImport = table.Column<bool>(type: "boolean", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultsChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TugenyTournamentId = table.Column<int>(type: "integer", nullable: true),
                    TugenySlug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TugenyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TugenyStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentResults_AspNetUsers_LastChangedByUserId",
                        column: x => x.LastChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentResults_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    SortIndex = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConnectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TugenyTeamId = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentPlacements_AspNetUsers_ConnectedByUserId",
                        column: x => x.ConnectedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentPlacements_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentPlacements_TournamentResults_TournamentResultId",
                        column: x => x.TournamentResultId,
                        principalTable: "TournamentResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortIndex = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SecondName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FirstPlacementId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecondPlacementId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstScores = table.Column<int[]>(type: "integer[]", nullable: false),
                    SecondScores = table.Column<int[]>(type: "integer[]", nullable: false),
                    Winner = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentPlacements_FirstPlacementId",
                        column: x => x.FirstPlacementId,
                        principalTable: "TournamentPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentPlacements_SecondPlacementId",
                        column: x => x.SecondPlacementId,
                        principalTable: "TournamentPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentResults_TournamentResultId",
                        column: x => x.TournamentResultId,
                        principalTable: "TournamentResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_FirstPlacementId",
                table: "TournamentMatches",
                column: "FirstPlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_SecondPlacementId",
                table: "TournamentMatches",
                column: "SecondPlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_TournamentResultId_SortIndex",
                table: "TournamentMatches",
                columns: new[] { "TournamentResultId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlacements_ConnectedByUserId",
                table: "TournamentPlacements",
                column: "ConnectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlacements_TeamId",
                table: "TournamentPlacements",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlacements_TournamentResultId_Position_SortIndex",
                table: "TournamentPlacements",
                columns: new[] { "TournamentResultId", "Position", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlacements_TournamentResultId_TeamId",
                table: "TournamentPlacements",
                columns: new[] { "TournamentResultId", "TeamId" },
                unique: true,
                filter: "\"TeamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentResults_EventId",
                table: "TournamentResults",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentResults_LastChangedByUserId",
                table: "TournamentResults",
                column: "LastChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentResults_TugenyTournamentId",
                table: "TournamentResults",
                column: "TugenyTournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentMatches");

            migrationBuilder.DropTable(
                name: "TournamentPlacements");

            migrationBuilder.DropTable(
                name: "TournamentResults");
        }
    }
}
