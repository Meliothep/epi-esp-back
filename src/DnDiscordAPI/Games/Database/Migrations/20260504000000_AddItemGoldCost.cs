using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscordAPI.Games.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddItemGoldCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GoldCost",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000001"),
                column: "GoldCost",
                value: 50);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000002"),
                column: "GoldCost",
                value: 50);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000003"),
                column: "GoldCost",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000004"),
                column: "GoldCost",
                value: 5);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000005"),
                column: "GoldCost",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000006"),
                column: "GoldCost",
                value: 25);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000007"),
                column: "GoldCost",
                value: 50);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000008"),
                column: "GoldCost",
                value: 75);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000009"),
                column: "GoldCost",
                value: 150);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-00000000000a"),
                column: "GoldCost",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-00000000000b"),
                column: "GoldCost",
                value: 200);

            migrationBuilder.UpdateData(
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-00000000000c"),
                column: "GoldCost",
                value: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoldCost",
                table: "Items");
        }
    }
}
