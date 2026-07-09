using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgesAndAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AchievementDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    AppliesToPlayers = table.Column<bool>(type: "boolean", nullable: false),
                    AppliesToTeams = table.Column<bool>(type: "boolean", nullable: false),
                    IsRetired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BadgeDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    AppliesToPlayers = table.Column<bool>(type: "boolean", nullable: false),
                    AppliesToTeams = table.Column<bool>(type: "boolean", nullable: false),
                    IsRetired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AchievementAwards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    ContextYear = table.Column<int>(type: "integer", nullable: true),
                    ContextLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementAwards", x => x.Id);
                    table.CheckConstraint("CK_AchievementAward_OneSubject", "(\"PlayerProfileId\" IS NOT NULL) <> (\"TeamId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AchievementAwards_AchievementDefinitions_AchievementDefinit~",
                        column: x => x.AchievementDefinitionId,
                        principalTable: "AchievementDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AchievementAwards_AspNetUsers_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AchievementAwards_PlayerProfiles_PlayerProfileId",
                        column: x => x.PlayerProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AchievementAwards_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AchievementIcons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementIcons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AchievementIcons_AchievementDefinitions_AchievementDefiniti~",
                        column: x => x.AchievementDefinitionId,
                        principalTable: "AchievementDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BadgeAwards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeAwards", x => x.Id);
                    table.CheckConstraint("CK_BadgeAward_OneSubject", "(\"PlayerProfileId\" IS NOT NULL) <> (\"TeamId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_BadgeAwards_AspNetUsers_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BadgeAwards_BadgeDefinitions_BadgeDefinitionId",
                        column: x => x.BadgeDefinitionId,
                        principalTable: "BadgeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BadgeAwards_PlayerProfiles_PlayerProfileId",
                        column: x => x.PlayerProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BadgeAwards_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BadgeIcons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeIcons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeIcons_BadgeDefinitions_BadgeDefinitionId",
                        column: x => x.BadgeDefinitionId,
                        principalTable: "BadgeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AchievementAwards_AchievementDefinitionId_PlayerProfileId",
                table: "AchievementAwards",
                columns: new[] { "AchievementDefinitionId", "PlayerProfileId" },
                unique: true,
                filter: "\"Status\" = 0 AND \"PlayerProfileId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AchievementAwards_AchievementDefinitionId_TeamId",
                table: "AchievementAwards",
                columns: new[] { "AchievementDefinitionId", "TeamId" },
                unique: true,
                filter: "\"Status\" = 0 AND \"TeamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AchievementAwards_GrantedByUserId",
                table: "AchievementAwards",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AchievementAwards_PlayerProfileId",
                table: "AchievementAwards",
                column: "PlayerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AchievementAwards_TeamId",
                table: "AchievementAwards",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_AchievementDefinitions_IsRetired",
                table: "AchievementDefinitions",
                column: "IsRetired");

            migrationBuilder.CreateIndex(
                name: "IX_AchievementIcons_AchievementDefinitionId",
                table: "AchievementIcons",
                column: "AchievementDefinitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwards_BadgeDefinitionId_PlayerProfileId",
                table: "BadgeAwards",
                columns: new[] { "BadgeDefinitionId", "PlayerProfileId" },
                unique: true,
                filter: "\"Status\" = 0 AND \"PlayerProfileId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwards_BadgeDefinitionId_TeamId",
                table: "BadgeAwards",
                columns: new[] { "BadgeDefinitionId", "TeamId" },
                unique: true,
                filter: "\"Status\" = 0 AND \"TeamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwards_GrantedByUserId",
                table: "BadgeAwards",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwards_PlayerProfileId",
                table: "BadgeAwards",
                column: "PlayerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwards_TeamId",
                table: "BadgeAwards",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeDefinitions_IsRetired",
                table: "BadgeDefinitions",
                column: "IsRetired");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeIcons_BadgeDefinitionId",
                table: "BadgeIcons",
                column: "BadgeDefinitionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AchievementAwards");

            migrationBuilder.DropTable(
                name: "AchievementIcons");

            migrationBuilder.DropTable(
                name: "BadgeAwards");

            migrationBuilder.DropTable(
                name: "BadgeIcons");

            migrationBuilder.DropTable(
                name: "AchievementDefinitions");

            migrationBuilder.DropTable(
                name: "BadgeDefinitions");
        }
    }
}
