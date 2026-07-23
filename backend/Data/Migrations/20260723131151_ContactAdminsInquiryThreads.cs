using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContactAdminsInquiryThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_TeamId",
                table: "Conversations");

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequesterUserId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_EventId_RequesterUserId",
                table: "Conversations",
                columns: new[] { "EventId", "RequesterUserId" },
                unique: true,
                filter: "\"Kind\" = 5");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_RequesterUserId",
                table: "Conversations",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TeamId",
                table: "Conversations",
                column: "TeamId",
                unique: true,
                filter: "\"TeamId\" IS NOT NULL AND \"Kind\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TeamId_RequesterUserId",
                table: "Conversations",
                columns: new[] { "TeamId", "RequesterUserId" },
                unique: true,
                filter: "\"Kind\" = 4");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AspNetUsers_RequesterUserId",
                table: "Conversations",
                column: "RequesterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Events_EventId",
                table: "Conversations",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AspNetUsers_RequesterUserId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Events_EventId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_EventId_RequesterUserId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_RequesterUserId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_TeamId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_TeamId_RequesterUserId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "RequesterUserId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TeamId",
                table: "Conversations",
                column: "TeamId",
                unique: true,
                filter: "\"TeamId\" IS NOT NULL");
        }
    }
}
