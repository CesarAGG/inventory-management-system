using InventoryManagementSystem.Data;
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

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    public string? CustomIdFormat { get; set; }

    [ForeignKey("OwnerId")]
    public virtual ApplicationUser? Owner { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
    public virtual ICollection<CustomField> CustomFields { get; set; } = new List<CustomField>();
}