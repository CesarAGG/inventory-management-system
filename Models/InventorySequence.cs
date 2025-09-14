using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementSystem.Models;

/// <summary>
/// Tracks the last used value for a specific sequence segment within an inventory.
/// </summary>
public class InventorySequence
{
    // Composite Primary Key defined in DbContext: (InventoryId, SegmentId)

    [Required]
    public string InventoryId { get; set; } = string.Empty;

    [Required]
    [StringLength(36)] // Matches the GUID string length of the segment ID
    public string SegmentId { get; set; } = string.Empty;

    [Required]
    public int LastValue { get; set; }

    // Navigation Properties
    [ForeignKey("InventoryId")]
    public virtual Inventory? Inventory { get; set; }
}