using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementSystem.Models;

public enum CustomFieldType { String, Text, Int, Bool, DateTime, FileUrl }

public class CustomField
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public CustomFieldType Type { get; set; }

    [Required]
    public string InventoryId { get; set; } = string.Empty;

    [ForeignKey("InventoryId")]
    public virtual Inventory? Inventory { get; set; }
}