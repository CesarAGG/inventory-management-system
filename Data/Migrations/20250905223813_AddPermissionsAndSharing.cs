using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionsAndSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_AspNetUsers_OwnerId",
                table: "Inventories");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Inventories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "InventoryUserPermissions",
                columns: table => new
                {
                    InventoryId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryUserPermissions", x => new { x.InventoryId, x.UserId });
                    table.ForeignKey(
                        name: "FK_InventoryUserPermissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryUserPermissions_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryUserPermissions_UserId",
                table: "InventoryUserPermissions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_AspNetUsers_OwnerId",
                table: "Inventories",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_AspNetUsers_OwnerId",
                table: "Inventories");

            migrationBuilder.DropTable(
                name: "InventoryUserPermissions");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Inventories");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_AspNetUsers_OwnerId",
                table: "Inventories",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
