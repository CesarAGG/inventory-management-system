using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeItemSchemaAndData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new system-managed CreatedAt column
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            // Drop the unused user-definable DateTime columns
            migrationBuilder.DropColumn(name: "CustomDateTime1", table: "Items");
            migrationBuilder.DropColumn(name: "CustomDateTime2", table: "Items");
            migrationBuilder.DropColumn(name: "CustomDateTime3", table: "Items");

            // Rename and change type using explicit SQL with the correct unconstrained numeric type
            migrationBuilder.Sql(@"
        ALTER TABLE ""Items"" RENAME COLUMN ""CustomInt1"" TO ""CustomNumeric1"";
        ALTER TABLE ""Items"" ALTER COLUMN ""CustomNumeric1"" TYPE numeric;
        ALTER TABLE ""Items"" ALTER COLUMN ""CustomNumeric1"" DROP NOT NULL;
    ");
            migrationBuilder.Sql(@"
        ALTER TABLE ""Items"" RENAME COLUMN ""CustomInt2"" TO ""CustomNumeric2"";
        ALTER TABLE ""Items"" ALTER COLUMN ""CustomNumeric2"" TYPE numeric;
        ALTER TABLE ""Items"" ALTER COLUMN ""CustomNumeric2"" DROP NOT NULL;
    ");
            migrationBuilder.Sql(@"
        ALTER TABLE ""Items"" RENAME COLUMN ""CustomInt3"" TO ""CustomNumeric3"";
        ALTER TABLE ""Items"" ALTER COLUMN ""CustomNumeric3"" TYPE numeric;
        ALTER TABLE ""Items"" ALTER COLUMN ""CustomNumeric3"" DROP NOT NULL;
    ");

            // Fix the stale TargetColumn data in the CustomFields table robustly
            migrationBuilder.Sql(@"
        UPDATE ""CustomFields""
        SET ""TargetColumn"" = REPLACE(""TargetColumn"", 'CustomInt', 'CustomNumeric')
        WHERE ""TargetColumn"" LIKE 'CustomInt%';
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Items");

            migrationBuilder.Sql(@"
        UPDATE ""CustomFields""
        SET ""TargetColumn"" = REPLACE(""TargetColumn"", 'CustomNumeric', 'CustomInt')
        WHERE ""TargetColumn"" LIKE 'CustomNumeric%';
    ");

            migrationBuilder.RenameColumn(
                name: "CustomNumeric1",
                table: "Items",
                newName: "CustomInt1");

            migrationBuilder.RenameColumn(
                name: "CustomNumeric2",
                table: "Items",
                newName: "CustomInt2");

            migrationBuilder.RenameColumn(
                name: "CustomNumeric3",
                table: "Items",
                newName: "CustomInt3");

            migrationBuilder.AlterColumn<int>(
                name: "CustomInt1",
                table: "Items",
                type: "integer",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CustomInt2",
                table: "Items",
                type: "integer",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CustomInt3",
                table: "Items",
                type: "integer",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomDateTime1",
                table: "Items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomDateTime2",
                table: "Items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomDateTime3",
                table: "Items",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
