using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscord.Campaign.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignCrudFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Campaigns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Campaigns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteCodeExpiresAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxPlayers",
                table: "Campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.CreateTable(
                name: "CampaignMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_InviteCode",
                table: "Campaigns",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_IsDeleted",
                table: "Campaigns",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_IsPublic",
                table: "Campaigns",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CampaignId",
                table: "CampaignMembers",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CampaignId_UserId",
                table: "CampaignMembers",
                columns: new[] { "CampaignId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_Status",
                table: "CampaignMembers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_UserId",
                table: "CampaignMembers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignMembers");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_InviteCode",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_IsDeleted",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_IsPublic",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "InviteCodeExpiresAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "MaxPlayers",
                table: "Campaigns");
        }
    }
}
