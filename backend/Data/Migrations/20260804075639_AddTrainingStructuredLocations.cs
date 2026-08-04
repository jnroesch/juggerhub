using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingStructuredLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                table: "Trainings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Trainings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "Trainings",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VenueName",
                table: "Trainings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CityIdOverride",
                table: "TrainingSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCodeOverride",
                table: "TrainingSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetOverride",
                table: "TrainingSessions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VenueNameOverride",
                table: "TrainingSessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trainings_CityId",
                table: "Trainings",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_CityIdOverride",
                table: "TrainingSessions",
                column: "CityIdOverride");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingSessions_Cities_CityIdOverride",
                table: "TrainingSessions",
                column: "CityIdOverride",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trainings_Cities_CityId",
                table: "Trainings",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainingSessions_Cities_CityIdOverride",
                table: "TrainingSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Trainings_Cities_CityId",
                table: "Trainings");

            migrationBuilder.DropIndex(
                name: "IX_Trainings_CityId",
                table: "Trainings");

            migrationBuilder.DropIndex(
                name: "IX_TrainingSessions_CityIdOverride",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "VenueName",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "CityIdOverride",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "PostalCodeOverride",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "StreetOverride",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "VenueNameOverride",
                table: "TrainingSessions");
        }
    }
}
