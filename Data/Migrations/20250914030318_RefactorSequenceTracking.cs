using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorSequenceTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create the new table that will be the destination for the data.
            migrationBuilder.CreateTable(
                name: "InventorySequences",
                columns: table => new
                {
                    InventoryId = table.Column<string>(type: "text", nullable: false),
                    SegmentId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    LastValue = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySequences", x => new { x.InventoryId, x.SegmentId });
                    table.ForeignKey(
                        name: "FK_InventorySequences_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Step 2: Manually migrate the data from the old column to the new table using raw SQL.
            // This SQL finds the first sequence segment in an inventory's format and uses its ID.
            // This is safe because the old architecture only supported one sequence anyway.
            migrationBuilder.Sql(@"
                INSERT INTO ""InventorySequences"" (""InventoryId"", ""SegmentId"", ""LastValue"")
                SELECT
                    i.""Id"" AS ""InventoryId"",
                    (elem->>'id')::text AS ""SegmentId"",
                    i.""LastSequenceValue""
                FROM
                    ""Inventories"" i,
                    jsonb_array_elements(i.""CustomIdFormat""::jsonb) elem
                WHERE
                    i.""CustomIdFormat"" IS NOT NULL
                    AND elem->>'type' = 'Sequence'
                    AND i.""LastSequenceValue"" > 0;
            ");

            // Step 3: Only after the data is safely migrated, drop the old column.
            migrationBuilder.DropColumn(
                name: "LastSequenceValue",
                table: "Inventories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Re-add the old column, allowing it to be nullable temporarily.
            migrationBuilder.AddColumn<int>(
                name: "LastSequenceValue",
                table: "Inventories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Step 2: Manually migrate the data back. If multiple sequences existed for an inventory,
            // we restore the highest value to be safe and prevent duplicate IDs.
            migrationBuilder.Sql(@"
                UPDATE ""Inventories"" i
                SET ""LastSequenceValue"" = (
                    SELECT MAX(s.""LastValue"")
                    FROM ""InventorySequences"" s
                    WHERE s.""InventoryId"" = i.""Id""
                )
                WHERE EXISTS (
                    SELECT 1
                    FROM ""InventorySequences"" s
                    WHERE s.""InventoryId"" = i.""Id""
                );
            ");

            // Step 3: Only after the data is safely restored, drop the new table.
            migrationBuilder.DropTable(
                name: "InventorySequences");
        }
    }
}