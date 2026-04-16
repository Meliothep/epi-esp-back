using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscord.Campaign.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignTreeDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CampaignTreeDefinition",
                table: "Campaigns",
                type: "json",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampaignTreeDefinition",
                table: "Campaigns");
        }
    }
}
