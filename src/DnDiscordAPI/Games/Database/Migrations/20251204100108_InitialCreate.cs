using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscordAPI.Games.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Race = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Abilities_Strength = table.Column<int>(type: "integer", nullable: false),
                    Abilities_Dexterity = table.Column<int>(type: "integer", nullable: false),
                    Abilities_Constitution = table.Column<int>(type: "integer", nullable: false),
                    Abilities_Intelligence = table.Column<int>(type: "integer", nullable: false),
                    Abilities_Wisdom = table.Column<int>(type: "integer", nullable: false),
                    Abilities_Charisma = table.Column<int>(type: "integer", nullable: false),
                    MaxHitPoints = table.Column<int>(type: "integer", nullable: false),
                    CurrentHitPoints = table.Column<int>(type: "integer", nullable: false),
                    ArmorClass = table.Column<int>(type: "integer", nullable: false),
                    Speed = table.Column<int>(type: "integer", nullable: false),
                    Initiative = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_DiscordUserId",
                table: "Characters",
                column: "DiscordUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Characters");
        }
    }
}
