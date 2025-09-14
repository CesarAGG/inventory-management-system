using InventoryManagementSystem.Services;
using InventoryManagementSystem.Services.InventoryServices;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory")]
public class ItemsApiController : ApiBaseController
{
    private readonly IItemService _itemService;

    public ItemsApiController(IItemService itemService)
    {
        _itemService = itemService;
    }

    [HttpGet("{inventoryId}/schema")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInventorySchema(string inventoryId)
    {
        var result = await _itemService.GetInventorySchemaAsync(inventoryId);
        return HandleServiceResult(result);
    }

    [HttpPost("{inventoryId}/items-data")]
    [AllowAnonymous]
    public async Task<IActionResult> LoadItemsForDataTable(string inventoryId, [FromForm] DataTablesRequest request)
    {
        var result = await _itemService.GetItemsForDataTableAsync(inventoryId, request);
        return HandleServiceResult(result);
    }

    [HttpPost("{inventoryId}/regenerate-id")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateId(string inventoryId, [FromBody] RegenerateIdRequest request)
    {
        var result = await _itemService.RegenerateIdAsync(inventoryId, request, User);
        if (!result.IsSuccess)
        {
            return HandleServiceResult(result);
        }

        return Ok(new { id = result.Data.Id, boundaries = result.Data.Boundaries, newSequenceValue = result.Data.NewSequenceValue });
    }

    [HttpPost("{inventoryId}/validate-id")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidateId(string inventoryId, [FromBody] ValidateIdRequest request)
    {
        var result = await _itemService.ValidateCustomIdAsync(inventoryId, request, User);
        return HandleServiceResult(result);
    }

    [HttpPost("{inventoryId}/items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(string inventoryId, [FromBody] ItemApiRequest request)
    {
        var result = await _itemService.CreateItemAsync(inventoryId, request, User);
        if (!result.IsSuccess)
        {
            return HandleServiceResult(result);
        }
        return Ok(result.Data);
    }

    [HttpPost("items/{itemId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(string itemId, [FromBody] ItemApiRequest request)
    {
        var result = await _itemService.UpdateItemAsync(itemId, request, User);
        return HandleServiceResult(result);
    }

    [HttpPost("items/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItems([FromBody] string[] itemIds)
    {
        var result = await _itemService.DeleteItemsAsync(itemIds, User);
        return HandleServiceResult(result);
    }
}