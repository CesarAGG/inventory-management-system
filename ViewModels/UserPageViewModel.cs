using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels;

public class InventoryInfoViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsPublic { get; set; }
    public int ItemCount { get; set; }
    public string? OwnerEmail { get; set; }
}

public class UserPageViewModel
{
    public IEnumerable<InventoryInfoViewModel> OwnedInventories { get; set; } = new List<InventoryInfoViewModel>();
    public IEnumerable<InventoryInfoViewModel> SharedInventories { get; set; } = new List<InventoryInfoViewModel>();
}