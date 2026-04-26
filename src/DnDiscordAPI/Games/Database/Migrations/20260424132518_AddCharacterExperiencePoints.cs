using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscordAPI.Games.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterExperiencePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExperiencePoints",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExperiencePoints",
                table: "Characters");
        }
    }
}
