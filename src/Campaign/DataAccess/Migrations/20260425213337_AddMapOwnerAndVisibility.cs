using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscord.Campaign.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddMapOwnerAndVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMaps_Campaigns_CampaignId",
                table: "CampaignMaps");

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignId",
                table: "CampaignMaps",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "CampaignMaps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "CampaignMaps",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMaps_IsPublic",
                table: "CampaignMaps",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMaps_OwnerId",
                table: "CampaignMaps",
                column: "OwnerId");

            // Backfill OwnerId from the campaign's DungeonMasterId for all existing
            // campaign-scoped maps. Standalone maps created after this migration will
            // have OwnerId set directly by UserMapService.CreateAsync.
            migrationBuilder.Sql(@"
                UPDATE ""CampaignMaps"" m
                SET ""OwnerId"" = c.""DungeonMasterId""
                FROM ""Campaigns"" c
                WHERE m.""CampaignId"" = c.""Id""
                  AND m.""OwnerId"" = '00000000-0000-0000-0000-000000000000';
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMaps_Campaigns_CampaignId",
                table: "CampaignMaps",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMaps_Campaigns_CampaignId",
                table: "CampaignMaps");

            migrationBuilder.DropIndex(
                name: "IX_CampaignMaps_IsPublic",
                table: "CampaignMaps");

            migrationBuilder.DropIndex(
                name: "IX_CampaignMaps_OwnerId",
                table: "CampaignMaps");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "CampaignMaps");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "CampaignMaps");

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignId",
                table: "CampaignMaps",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMaps_Campaigns_CampaignId",
                table: "CampaignMaps",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
