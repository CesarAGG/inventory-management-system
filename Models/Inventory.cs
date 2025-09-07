using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementSystem.Models;

public class Inventory
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty; // The name of the inventory itself

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPublic { get; set; } = false;

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    public string? CustomIdFormat { get; set; }

    public int LastSequenceValue { get; set; } = 0;

    public string? CustomIdFormatHash { get; set; }

    public uint Version { get; set; }

    [ForeignKey("OwnerId")]
    public virtual ApplicationUser? Owner { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
    public virtual ICollection<CustomField> CustomFields { get; set; } = new List<CustomField>();
    public virtual ICollection<InventoryUserPermission> Permissions { get; set; } = new List<InventoryUserPermission>();
}