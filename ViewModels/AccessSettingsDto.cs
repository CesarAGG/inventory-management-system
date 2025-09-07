using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels;

public class AccessSettingsDto
{
    public bool IsPublic { get; set; }
    public List<UserPermissionDto> Permissions { get; set; } = new();
}