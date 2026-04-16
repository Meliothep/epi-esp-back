using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscord.Campaign.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignGameSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignGameSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentNodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignGameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignGameSessions_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NodeTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PortUsed = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ChoiceText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VisitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionHistoryEntries_CampaignGameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "CampaignGameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_CampaignGameSessions_CampaignId", table: "CampaignGameSessions", column: "CampaignId");
            migrationBuilder.CreateIndex(name: "IX_CampaignGameSessions_StartedAt", table: "CampaignGameSessions", column: "StartedAt");
            migrationBuilder.CreateIndex(name: "IX_CampaignGameSessions_StartedBy", table: "CampaignGameSessions", column: "StartedBy");
            migrationBuilder.CreateIndex(name: "IX_CampaignGameSessions_Status", table: "CampaignGameSessions", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_SessionHistoryEntries_SessionId", table: "SessionHistoryEntries", column: "SessionId");
            migrationBuilder.CreateIndex(name: "IX_SessionHistoryEntries_VisitedAt", table: "SessionHistoryEntries", column: "VisitedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SessionHistoryEntries");
            migrationBuilder.DropTable(name: "CampaignGameSessions");
        }
    }
}
