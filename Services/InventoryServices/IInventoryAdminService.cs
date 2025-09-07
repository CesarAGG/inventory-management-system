using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using InventoryManagementSystem.Models.CustomId;
using InventoryManagementSystem.ViewModels;

namespace InventoryManagementSystem.Services.InventoryServices;

public interface IInventoryAdminService
{
    Task<ServiceResult<IEnumerable<IdSegment>>> GetIdFormatAsync(string inventoryId, ClaimsPrincipal user);
    Task<ServiceResult<object>> SaveIdFormatAsync(string inventoryId, JsonElement format, ClaimsPrincipal user);
    Task<ServiceResult<object>> RenameInventoryAsync(string inventoryId, RenameInventoryRequest request, ClaimsPrincipal user);
    Task<ServiceResult<object>> TransferOwnershipAsync(string inventoryId, TransferOwnershipRequest request, ClaimsPrincipal user);
    Task<ServiceResult<object>> DeleteInventoryAsync(string inventoryId, ClaimsPrincipal user);
}