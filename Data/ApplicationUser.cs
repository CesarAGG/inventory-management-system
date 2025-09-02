using Microsoft.AspNetCore.Identity;

namespace InventoryManagementSystem.Data;

public class ApplicationUser : IdentityUser
{
    public bool IsBlocked { get; set; } = false;
}