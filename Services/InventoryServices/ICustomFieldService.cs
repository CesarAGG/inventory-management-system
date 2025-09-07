using InventoryManagementSystem.ViewModels;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services.InventoryServices;

public interface ICustomFieldService
{
    Task<(CustomFieldDto? Field, object? Error)> AddCustomFieldAsync(string inventoryId, CustomFieldDto newField, ClaimsPrincipal user);
    Task<(List<CustomFieldDto>? Fields, object? Error)> GetCustomFieldsAsync(string inventoryId, ClaimsPrincipal user);
    Task<object?> UpdateCustomFieldAsync(string fieldId, CustomFieldDto fieldUpdate, ClaimsPrincipal user);
    Task<object?> DeleteCustomFieldsAsync(string[] fieldIds, ClaimsPrincipal user);
    Task<object?> ReorderCustomFieldsAsync(string inventoryId, string[] orderedFieldIds, ClaimsPrincipal user);
}