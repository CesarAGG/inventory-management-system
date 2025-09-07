using InventoryManagementSystem.ViewModels;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services.InventoryServices;

public interface IItemService
{
    Task<(object? Data, string? Error)> GetItemsDataAsync(string inventoryId);
    Task<(ItemDto? Item, object? Error)> CreateItemAsync(string inventoryId, ItemApiRequest request, ClaimsPrincipal user);
    Task<(object? UpdatedItem, object? Error)> UpdateItemAsync(string itemId, ItemApiRequest request, ClaimsPrincipal user);
    Task<object?> DeleteItemsAsync(string[] itemIds, ClaimsPrincipal user);
}