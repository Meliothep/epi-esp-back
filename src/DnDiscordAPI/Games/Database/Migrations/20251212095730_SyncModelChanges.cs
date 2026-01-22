using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscordAPI.Games.Database.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop and recreate columns to avoid type conversion issues
            migrationBuilder.Sql(@"ALTER TABLE ""Characters"" DROP COLUMN IF EXISTS ""Race"" CASCADE;");
            migrationBuilder.Sql(@"ALTER TABLE ""Characters"" ADD COLUMN ""Race"" integer NOT NULL DEFAULT 0;");

            migrationBuilder.Sql(@"ALTER TABLE ""Characters"" DROP COLUMN IF EXISTS ""Class"" CASCADE;");
            migrationBuilder.Sql(@"ALTER TABLE ""Characters"" ADD COLUMN ""Class"" integer NOT NULL DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Race",
                table: "Characters",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Class",
                table: "Characters",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
