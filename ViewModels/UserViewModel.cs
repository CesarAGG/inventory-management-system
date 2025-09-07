using InventoryManagementSystem.Data;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels;

public class UserViewModel
{
    public ApplicationUser User { get; set; } = new();
    public IList<string> Roles { get; set; } = new List<string>();
}