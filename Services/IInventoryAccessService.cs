using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services;

public interface IInventoryAccessService
{
    bool CanManageSettings(Inventory inventory, string userId, bool isAdmin);
    Task<bool> CanWrite(Inventory inventory, string userId, bool isAdmin);
}