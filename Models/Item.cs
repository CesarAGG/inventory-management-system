using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementSystem.Models;

public class Item
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CustomId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CustomIdFormatHashApplied { get; set; }

    public string? CustomIdSegmentBoundaries { get; set; }

    public uint Version { get; set; }

    public string? CustomString1 { get; set; }
    public string? CustomString2 { get; set; }
    public string? CustomString3 { get; set; }

    public string? CustomText1 { get; set; }
    public string? CustomText2 { get; set; }
    public string? CustomText3 { get; set; }

    public decimal? CustomNumeric1 { get; set; }
    public decimal? CustomNumeric2 { get; set; }
    public decimal? CustomNumeric3 { get; set; }

    public bool? CustomBool1 { get; set; }
    public bool? CustomBool2 { get; set; }
    public bool? CustomBool3 { get; set; }

    public string? CustomFileUrl1 { get; set; }
    public string? CustomFileUrl2 { get; set; }
    public string? CustomFileUrl3 { get; set; }

    [Required]
    public string InventoryId { get; set; } = string.Empty;

    [ForeignKey("InventoryId")]
    public virtual Inventory? Inventory { get; set; }
}