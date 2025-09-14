using InventoryManagementSystem.Services;
using InventoryManagementSystem.Services.InventoryServices;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Policy;
using System.Text.Json;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory/{inventoryId}")]
public class InventorySettingsApiController : ApiBaseController
{
    private readonly IInventoryAdminService _inventoryAdminService;

    public InventorySettingsApiController(IInventoryAdminService inventoryAdminService)
    {
        _inventoryAdminService = inventoryAdminService;
    }

    [HttpGet("id-format")]
    [AllowAnonymous] // Kept as per original controller
    public async Task<IActionResult> GetIdFormat(string inventoryId)
    {
        var result = await _inventoryAdminService.GetIdFormatAsync(inventoryId, User);
        if (!result.IsSuccess)
        {
            return HandleServiceResult(result);
        }
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var jsonString = JsonSerializer.Serialize(result.Data!, options);
        return Content(jsonString, "application/json");
    }

    [HttpPut("id-format")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIdFormat(string inventoryId, [FromBody] JsonElement format)
    {
        var result = await _inventoryAdminService.SaveIdFormatAsync(inventoryId, format, User);
        return HandleServiceResult(result);
    }

    [HttpPut("rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameInventory(string inventoryId, [FromBody] RenameInventoryRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _inventoryAdminService.RenameInventoryAsync(inventoryId, request, User);
        return HandleServiceResult(result);
    }

    [HttpPost("transfer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferOwnership(string inventoryId, [FromBody] TransferOwnershipRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _inventoryAdminService.TransferOwnershipAsync(inventoryId, request, User);
        if (!result.IsSuccess)
        {
            return HandleServiceResult(result);
        }

        var redirectUrl = result.Data!.ShouldRedirect ? Url.Action("Index", "User") : null;
        return Ok(new { message = result.Data.Message, redirectUrl, newVersion = result.Data.NewInventoryVersion });
    }

    [HttpDelete("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInventory(string inventoryId)
    {
        var result = await _inventoryAdminService.DeleteInventoryAsync(inventoryId, User);
        return HandleServiceResult(result);
    }
}