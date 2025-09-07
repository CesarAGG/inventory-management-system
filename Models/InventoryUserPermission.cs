using InventoryManagementSystem.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementSystem.Models;

public enum PermissionLevel
{
    Write
}

public class InventoryUserPermission
{
    // Composite Key defined in DbContext
    public string InventoryId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    public PermissionLevel Level { get; set; }

    // Navigation Properties
    [ForeignKey("InventoryId")]
    public virtual Inventory? Inventory { get; set; }

    [ForeignKey("UserId")]
    public virtual ApplicationUser? User { get; set; }
}