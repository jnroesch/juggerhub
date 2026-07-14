using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuggerHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RosterCap",
                table: "Events",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RosterCap = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EventSignupId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parties_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Parties_EventSignups_EventSignupId",
                        column: x => x.EventSignupId,
                        principalTable: "EventSignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Parties_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Parties_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyAdminInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyAdminInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyAdminInvitations_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartyAdminInvitations_AspNetUsers_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartyAdminInvitations_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyMembers_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyNewsPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyNewsPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyNewsPosts_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartyNewsPosts_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_CreatedByUserId",
                table: "Parties",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_EventId",
                table: "Parties",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_EventSignupId",
                table: "Parties",
                column: "EventSignupId",
                unique: true,
                filter: "\"EventSignupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_TeamId",
                table: "Parties",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_TeamId_EventId",
                table: "Parties",
                columns: new[] { "TeamId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyAdminInvitations_CreatedByUserId",
                table: "PartyAdminInvitations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyAdminInvitations_PartyId",
                table: "PartyAdminInvitations",
                column: "PartyId",
                unique: true,
                filter: "\"Kind\" = 0 AND \"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PartyAdminInvitations_PartyId_TargetUserId",
                table: "PartyAdminInvitations",
                columns: new[] { "PartyId", "TargetUserId" },
                unique: true,
                filter: "\"Kind\" = 1 AND \"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PartyAdminInvitations_TargetUserId",
                table: "PartyAdminInvitations",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyAdminInvitations_Token",
                table: "PartyAdminInvitations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyMembers_PartyId_Status",
                table: "PartyMembers",
                columns: new[] { "PartyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PartyMembers_PartyId_UserId",
                table: "PartyMembers",
                columns: new[] { "PartyId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyMembers_UserId",
                table: "PartyMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyNewsPosts_AuthorUserId",
                table: "PartyNewsPosts",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyNewsPosts_PartyId_CreatedDate",
                table: "PartyNewsPosts",
                columns: new[] { "PartyId", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartyAdminInvitations");

            migrationBuilder.DropTable(
                name: "PartyMembers");

            migrationBuilder.DropTable(
                name: "PartyNewsPosts");

            migrationBuilder.DropTable(
                name: "Parties");

            migrationBuilder.DropColumn(
                name: "RosterCap",
                table: "Events");
        }
    }
}
