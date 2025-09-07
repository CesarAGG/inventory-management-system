using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services;

public class InventoryAccessService : IInventoryAccessService
{
    private readonly ApplicationDbContext _context;

    public InventoryAccessService(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool CanManageSettings(Inventory inventory, string userId, bool isAdmin)
    {
        return inventory.OwnerId == userId || isAdmin;
    }

    public async Task<bool> CanWrite(Inventory inventory, string userId, bool isAdmin)
    {
        if (string.IsNullOrEmpty(userId)) return false;

        if (CanManageSettings(inventory, userId, isAdmin) || inventory.IsPublic)
        {
            return true;
        }

        return await _context.InventoryUserPermissions
            .AsNoTracking()
            .AnyAsync(p => p.InventoryId == inventory.Id && p.UserId == userId && p.Level == PermissionLevel.Write);
    }
}