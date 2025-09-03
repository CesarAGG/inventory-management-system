using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompliantInventorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inventories_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomFields",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    InventoryId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFields_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CustomString1 = table.Column<string>(type: "text", nullable: true),
                    CustomString2 = table.Column<string>(type: "text", nullable: true),
                    CustomString3 = table.Column<string>(type: "text", nullable: true),
                    CustomText1 = table.Column<string>(type: "text", nullable: true),
                    CustomText2 = table.Column<string>(type: "text", nullable: true),
                    CustomText3 = table.Column<string>(type: "text", nullable: true),
                    CustomInt1 = table.Column<int>(type: "integer", nullable: true),
                    CustomInt2 = table.Column<int>(type: "integer", nullable: true),
                    CustomInt3 = table.Column<int>(type: "integer", nullable: true),
                    CustomBool1 = table.Column<bool>(type: "boolean", nullable: true),
                    CustomBool2 = table.Column<bool>(type: "boolean", nullable: true),
                    CustomBool3 = table.Column<bool>(type: "boolean", nullable: true),
                    CustomDateTime1 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomDateTime2 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomDateTime3 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomFileUrl1 = table.Column<string>(type: "text", nullable: true),
                    CustomFileUrl2 = table.Column<string>(type: "text", nullable: true),
                    CustomFileUrl3 = table.Column<string>(type: "text", nullable: true),
                    InventoryId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_InventoryId",
                table: "CustomFields",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_OwnerId",
                table: "Inventories",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_InventoryId",
                table: "Items",
                column: "InventoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFields");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "Inventories");
        }
    }
}
