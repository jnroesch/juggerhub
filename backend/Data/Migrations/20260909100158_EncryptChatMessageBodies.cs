using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <summary>
    /// Chat message bodies move from plaintext to an encryption envelope (feature 047 / #223).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Drop and add, not a conversion.</b> Existing rows keep their metadata and lose their
    /// text. That is deliberate and was the owner's explicit position: every environment holds test
    /// data only, so no backfill, no dual-read window and no compatibility with previously stored
    /// plaintext is required (FR-025). Altering the column in place would imply a conversion
    /// happened.
    /// </para>
    /// <para>
    /// <b><c>Down</c> cannot restore content.</b> It restores the column, empty. There is no
    /// plaintext left to put back — reverting this is a schema reversal, not a data one.
    /// </para>
    /// </remarks>
    public partial class EncryptChatMessageBodies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Body",
                table: "ChatMessages");

            migrationBuilder.AddColumn<byte[]>(
                name: "BodyCipher",
                table: "ChatMessages",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyCipher",
                table: "ChatMessages");

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "ChatMessages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }
    }
}
