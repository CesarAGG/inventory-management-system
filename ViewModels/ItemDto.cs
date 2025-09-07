using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels;

public class ItemDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object?> Fields { get; set; } = new();
    public uint NewInventoryVersion { get; set; } // New property
}