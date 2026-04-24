using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DnDiscordAPI.Games.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Wallet_CopperPieces",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Wallet_ElectrumPieces",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Wallet_GoldPieces",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Wallet_PlatinumPieces",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Wallet_SilverPieces",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    ModelUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryEntries_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Items",
                columns: new[] { "Id", "Category", "Description", "Icon", "ModelUrl", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-000000000001"), 0, "Une fiole emplie d'un liquide rouge vif qui semble bouillonner doucement, même au repos.", "potion-red", null, "Potion de soin" },
                    { new Guid("11111111-1111-1111-1111-000000000002"), 0, "Un flacon au liquide bleu nuit parcouru d'éclats scintillants, comme un ciel étoilé en miniature.", "potion-blue", null, "Potion de mana" },
                    { new Guid("11111111-1111-1111-1111-000000000003"), 3, "Un bâton de bois enduit de résine à son extrémité, prêt à être allumé pour éclairer les ténèbres.", "torch", null, "Torche" },
                    { new Guid("11111111-1111-1111-1111-000000000004"), 0, "Un paquet soigneusement emballé contenant du pain dur, de la viande séchée et un morceau de fromage.", "bread", null, "Ration de voyage" },
                    { new Guid("11111111-1111-1111-1111-000000000005"), 3, "Une longue corde en chanvre tressé, solide et fiable, indispensable pour tout aventurier.", "rope", null, "Corde en chanvre" },
                    { new Guid("11111111-1111-1111-1111-000000000006"), 1, "Une lame courte et effilée, parfaite pour les coups rapides ou un lancer précis.", "dagger", null, "Dague affûtée" },
                    { new Guid("11111111-1111-1111-1111-000000000007"), 1, "Un arc léger en bois d'if, idéal pour la chasse et les escarmouches à distance.", "bow", null, "Arc court" },
                    { new Guid("11111111-1111-1111-1111-000000000008"), 2, "Un bouclier rond en acier forgé, marqué par les coups de batailles passées.", "shield", null, "Bouclier en acier" },
                    { new Guid("11111111-1111-1111-1111-000000000009"), 4, "Un vieux parchemin couvert de runes argentées qui luisent faiblement dans l'obscurité.", "scroll", null, "Parchemin de sort" },
                    { new Guid("11111111-1111-1111-1111-00000000000a"), 5, "Un petit coffre en bois cerclé de fer, fermé par un cadenas rouillé. On entend quelque chose tinter à l'intérieur.", "chest", null, "Coffre au trésor" },
                    { new Guid("11111111-1111-1111-1111-00000000000b"), 4, "Un pendentif en bronze patiné, orné d'un œil gravé qui semble suivre du regard quiconque le porte.", "amulet", null, "Amulette ancienne" },
                    { new Guid("11111111-1111-1111-1111-00000000000c"), 5, "Un parchemin jauni et usé, marqué d'un grand X rouge. L'encre semble ancienne mais la carte reste lisible.", "map", null, "Carte au trésor" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryEntries_CharacterId",
                table: "InventoryEntries",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryEntries_CharacterId_ItemId",
                table: "InventoryEntries",
                columns: new[] { "CharacterId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryEntries_ItemId",
                table: "InventoryEntries",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryEntries");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropColumn(
                name: "Wallet_CopperPieces",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Wallet_ElectrumPieces",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Wallet_GoldPieces",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Wallet_PlatinumPieces",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Wallet_SilverPieces",
                table: "Characters");
        }
    }
}
