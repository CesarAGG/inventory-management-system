using System.Collections.Generic;

namespace InventoryManagementSystem.Controllers;

public class UserPermissionDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
}

public class AccessSettingsDto
{
    public bool IsPublic { get; set; }
    public List<UserPermissionDto> Permissions { get; set; } = new();
}

public class UserSearchDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}