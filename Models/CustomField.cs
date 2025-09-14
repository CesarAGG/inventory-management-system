using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementSystem.Models;

public enum CustomFieldType { String, Text, Numeric, Bool, FileUrl }

public class CustomField
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    public bool IsVisibleInTable { get; set; } = true;

    [Required]
    public CustomFieldType Type { get; set; }

    public int Order { get; set; } = 0;

    [Required]
    public string InventoryId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string TargetColumn { get; set; } = string.Empty;

    [ForeignKey("InventoryId")]
    public virtual Inventory? Inventory { get; set; }
}