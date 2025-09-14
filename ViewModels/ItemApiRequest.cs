using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels;

public class ItemApiRequest
{
    public string? CustomId { get; set; }
    public string? CustomIdSegmentBoundaries { get; set; }
    public Dictionary<string, object> FieldValues { get; set; } = new();
}