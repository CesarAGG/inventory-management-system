using InventoryManagementSystem.ViewModels;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.Services.InventoryServices;

public interface IItemService
{
    Task<ServiceResult<(string Id, string Boundaries)>> RegenerateIdAsync(string inventoryId, uint inventoryVersion, ClaimsPrincipal user);
    Task<ServiceResult<InventorySchemaViewModel>> GetInventorySchemaAsync(string inventoryId);
    Task<ServiceResult<DataTablesResponse<Dictionary<string, object?>>>> GetItemsForDataTableAsync(string inventoryId, DataTablesRequest request);
    Task<ServiceResult<ItemDto>> CreateItemAsync(string inventoryId, ItemApiRequest request, ClaimsPrincipal user);
    Task<ServiceResult<object>> UpdateItemAsync(string itemId, ItemApiRequest request, ClaimsPrincipal user);
    Task<ServiceResult<object>> DeleteItemsAsync(string[] itemIds, ClaimsPrincipal user);
}