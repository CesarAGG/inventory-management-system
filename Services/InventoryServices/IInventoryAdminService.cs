using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using InventoryManagementSystem.Models.CustomId;
using InventoryManagementSystem.ViewModels;

namespace InventoryManagementSystem.Services.InventoryServices;

public interface IInventoryAdminService
{
    Task<(IEnumerable<IdSegment>? Segments, object? Error)> GetIdFormatAsync(string inventoryId, ClaimsPrincipal user);
    Task<object?> SaveIdFormatAsync(string inventoryId, JsonElement format, ClaimsPrincipal user);
    Task<(string? NewName, object? Error)> RenameInventoryAsync(string inventoryId, RenameInventoryRequest request, ClaimsPrincipal user);
    Task<(string Message, bool ShouldRedirect, object? Error)> TransferOwnershipAsync(string inventoryId, TransferOwnershipRequest request, ClaimsPrincipal user);
    Task<object?> DeleteInventoryAsync(string inventoryId, ClaimsPrincipal user);
}