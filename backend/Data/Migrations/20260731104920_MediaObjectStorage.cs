using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class MediaObjectStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Feature 035 (#97) — cutover to object storage.
            //
            // Existing descriptor rows are DELETED, not migrated. The owner accepted total loss of
            // stored media in every environment (specs/035 Clarifications), which knowingly waives
            // the "Existing avatars migrated" criterion on GH #97. Two reasons the delete has to be
            // here rather than left out:
            //
            //   1. Correctness — a surviving row would carry an empty ObjectKey and point at an
            //      object that was never written, which is exactly the "record without object"
            //      state the spec forbids (FR-019). Members would see broken images rather than
            //      the ordinary placeholder.
            //   2. It would not even apply — the new unique index on ObjectKey cannot be created
            //      while two or more rows share the empty-string default.
            //
            // After this runs, every member and every catalogue definition is in the clean "never
            // had a picture" state: members can upload again, and an administrator re-uploads the
            // catalogue icons through the existing admin area.
            migrationBuilder.Sql("DELETE FROM \"ProfileAvatars\";");
            migrationBuilder.Sql("DELETE FROM \"BadgeIcons\";");
            migrationBuilder.Sql("DELETE FROM \"AchievementIcons\";");

            migrationBuilder.DropColumn(
                name: "Bytes",
                table: "ProfileAvatars");

            migrationBuilder.DropColumn(
                name: "Bytes",
                table: "BadgeIcons");

            migrationBuilder.DropColumn(
                name: "Bytes",
                table: "AchievementIcons");

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                table: "ProfileAvatars",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SizeBytes",
                table: "ProfileAvatars",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                table: "BadgeIcons",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SizeBytes",
                table: "BadgeIcons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                table: "AchievementIcons",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SizeBytes",
                table: "AchievementIcons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileAvatars_ObjectKey",
                table: "ProfileAvatars",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BadgeIcons_ObjectKey",
                table: "BadgeIcons",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AchievementIcons_ObjectKey",
                table: "AchievementIcons",
                column: "ObjectKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverting restores the byte columns but NOT any bytes — the objects live in the media
            // store and this migration never held them. Descriptor rows are dropped for the same
            // reason they are dropped on the way up: a row with an empty Bytes value describes an
            // image that does not exist. A revert therefore lands in the same clean empty state.
            migrationBuilder.Sql("DELETE FROM \"ProfileAvatars\";");
            migrationBuilder.Sql("DELETE FROM \"BadgeIcons\";");
            migrationBuilder.Sql("DELETE FROM \"AchievementIcons\";");

            migrationBuilder.DropIndex(
                name: "IX_ProfileAvatars_ObjectKey",
                table: "ProfileAvatars");

            migrationBuilder.DropIndex(
                name: "IX_BadgeIcons_ObjectKey",
                table: "BadgeIcons");

            migrationBuilder.DropIndex(
                name: "IX_AchievementIcons_ObjectKey",
                table: "AchievementIcons");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                table: "ProfileAvatars");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "ProfileAvatars");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                table: "BadgeIcons");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "BadgeIcons");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                table: "AchievementIcons");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "AchievementIcons");

            migrationBuilder.AddColumn<byte[]>(
                name: "Bytes",
                table: "ProfileAvatars",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "Bytes",
                table: "BadgeIcons",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "Bytes",
                table: "AchievementIcons",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
