using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscord.Campaign.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddRollHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RollHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DiceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RollHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rollhistory_campaign_rolledat",
                table: "RollHistory",
                columns: new[] { "CampaignId", "RolledAt" });

            migrationBuilder.CreateIndex(
                name: "ix_rollhistory_session",
                table: "RollHistory",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RollHistory");
        }
    }
}
