using InventoryManagementSystem.ViewModels;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.Services.InventoryServices;

public interface IItemService
{
    Task<ServiceResult<object>> GetItemsDataAsync(string inventoryId);
    Task<ServiceResult<ItemDto>> CreateItemAsync(string inventoryId, ItemApiRequest request, ClaimsPrincipal user);
    Task<ServiceResult<object>> UpdateItemAsync(string itemId, ItemApiRequest request, ClaimsPrincipal user);
    Task<ServiceResult<object>> DeleteItemsAsync(string[] itemIds, ClaimsPrincipal user);
}