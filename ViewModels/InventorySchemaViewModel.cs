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
    public string? Description { get; set; }
    public string Data { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Orderable { get; set; } = true;
    public bool IsVisibleInTable { get; set; } = true;
}