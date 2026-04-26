using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscordAPI.Games.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterAsiAppliedCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AsiAppliedCount",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE \"Characters\" SET \"AsiAppliedCount\" = \"Level\" / 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AsiAppliedCount",
                table: "Characters");
        }
    }
}
