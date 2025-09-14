using InventoryManagementSystem.Services;
using InventoryManagementSystem.Services.InventoryServices;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory")]
public class CustomFieldsApiController : ApiBaseController
{
    private readonly ICustomFieldService _customFieldService;

    public CustomFieldsApiController(ICustomFieldService customFieldService)
    {
        _customFieldService = customFieldService;
    }

    [HttpPost("{inventoryId}/fields")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomField(string inventoryId, [FromBody] CustomFieldDto newField)
    {
        var result = await _customFieldService.AddCustomFieldAsync(inventoryId, newField, User);
        if (!result.IsSuccess)
        {
            return HandleServiceResult(result);
        }
        return CreatedAtAction(nameof(GetCustomFields), new { inventoryId }, result.Data);
    }

    [HttpGet("{inventoryId}/fields")]
    public async Task<IActionResult> GetCustomFields(string inventoryId)
    {
        var result = await _customFieldService.GetCustomFieldsAsync(inventoryId, User);
        return HandleServiceResult(result);
    }

    [HttpPut("fields/{fieldId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCustomField(string fieldId, [FromBody] UpdateFieldRequest fieldUpdate)
    {
        var result = await _customFieldService.UpdateCustomFieldAsync(fieldId, fieldUpdate, User);
        return HandleServiceResult(result);
    }

    [HttpPost("fields/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomFields([FromBody] FieldDeleteRequest deleteRequest)
    {
        var result = await _customFieldService.DeleteCustomFieldsAsync(deleteRequest, User);
        return HandleServiceResult(result);
    }

    [HttpPut("{inventoryId}/fields/reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderCustomFields(string inventoryId, [FromBody] FieldReorderRequest reorderRequest)
    {
        var result = await _customFieldService.ReorderCustomFieldsAsync(inventoryId, reorderRequest, User);
        return HandleServiceResult(result);
    }
}