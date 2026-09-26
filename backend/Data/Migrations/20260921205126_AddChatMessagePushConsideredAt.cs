using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessagePushConsideredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PushConsideredAt",
                table: "ChatMessages",
                type: "timestamp with time zone",
                nullable: true);

            // Every message that already exists is marked as already considered, BEFORE the partial
            // index below is created. Two reasons, and both matter:
            //
            //  1. The index is partial on "PushConsideredAt" IS NULL. A pre-existing row is never
            //     selected by the pass (it fails the max-age filter) and is therefore never marked,
            //     so without this it would sit in that index permanently — and the index being small
            //     is the entire reason it is affordable to query every few seconds.
            //  2. Nobody is owed a notification about a conversation from last week. Without this,
            //     the first pass after deployment would consider the whole message history.
            //
            // Creating the index afterwards also means it is built over the handful of rows this
            // leaves behind rather than over the table.
            migrationBuilder.Sql(
                "UPDATE \"ChatMessages\" SET \"PushConsideredAt\" = now() WHERE \"PushConsideredAt\" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_PushConsideredAt_Pending",
                table: "ChatMessages",
                column: "CreatedDate",
                filter: "\"PushConsideredAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_PushConsideredAt_Pending",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "PushConsideredAt",
                table: "ChatMessages");
        }
    }
}
