using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscordAPI.Games.Database.Migrations
{
    /// <inheritdoc />
    public partial class InventoryUniqueIndexAndCharacterFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryEntries_CharacterId_ItemId",
                table: "InventoryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryEntries_CharacterId_ItemId",
                table: "InventoryEntries",
                columns: new[] { "CharacterId", "ItemId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryEntries_Characters_CharacterId",
                table: "InventoryEntries",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryEntries_Characters_CharacterId",
                table: "InventoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_InventoryEntries_CharacterId_ItemId",
                table: "InventoryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryEntries_CharacterId_ItemId",
                table: "InventoryEntries",
                columns: new[] { "CharacterId", "ItemId" });
        }
    }
}
