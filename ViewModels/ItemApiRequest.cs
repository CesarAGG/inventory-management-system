using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels;

public class ItemApiRequest
{
    public Dictionary<string, object> FieldValues { get; set; } = new();
}