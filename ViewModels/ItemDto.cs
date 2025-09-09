using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels;

public class ItemDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomId { get; set; } = string.Empty;
    public string? CustomIdSegmentBoundaries { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object?> Fields { get; set; } = new();
    public uint NewInventoryVersion { get; set; }
    public uint NewItemVersion { get; set; }
}