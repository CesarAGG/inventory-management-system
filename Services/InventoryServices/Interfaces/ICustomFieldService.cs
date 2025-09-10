using InventoryManagementSystem.ViewModels;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.Services.InventoryServices;

public interface ICustomFieldService
{
    Task<ServiceResult<CustomFieldDto>> AddCustomFieldAsync(string inventoryId, CustomFieldDto newField, ClaimsPrincipal user);
    Task<ServiceResult<List<CustomFieldDto>>> GetCustomFieldsAsync(string inventoryId, ClaimsPrincipal user);
    Task<ServiceResult<object>> UpdateCustomFieldAsync(string fieldId, UpdateFieldRequest fieldUpdate, ClaimsPrincipal user);
    Task<ServiceResult<object>> DeleteCustomFieldsAsync(FieldDeleteRequest deleteRequest, ClaimsPrincipal user);
    Task<ServiceResult<object>> ReorderCustomFieldsAsync(string inventoryId, FieldReorderRequest reorderRequest, ClaimsPrincipal user);
}