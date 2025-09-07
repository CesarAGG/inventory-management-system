using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels;

public class InventorySchemaViewModel
{
    public List<ColumnDefinition> Columns { get; set; } = new List<ColumnDefinition>();
}

public class ColumnDefinition
{
    public string FieldId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty; // This is the key for the data object
    public string Type { get; set; } = "string"; // Helps client-side rendering
    public bool Orderable { get; set; } = true;
}