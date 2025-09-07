using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Data;

public class ApplicationUser : IdentityUser
{
    public bool IsBlocked { get; set; } = false;
    public virtual ICollection<InventoryUserPermission> Permissions { get; set; } = new List<InventoryUserPermission>();
}