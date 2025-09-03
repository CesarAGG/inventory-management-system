using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetColumnAndUniqueConstraintToCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomFields_InventoryId",
                table: "CustomFields");

            migrationBuilder.AddColumn<string>(
                name: "TargetColumn",
                table: "CustomFields",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_InventoryId_TargetColumn",
                table: "CustomFields",
                columns: new[] { "InventoryId", "TargetColumn" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomFields_InventoryId_TargetColumn",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "TargetColumn",
                table: "CustomFields");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_InventoryId",
                table: "CustomFields",
                column: "InventoryId");
        }
    }
}
