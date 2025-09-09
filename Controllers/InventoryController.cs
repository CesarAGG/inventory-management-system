using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Text.Json;
using InventoryManagementSystem.ViewModels;
using InventoryManagementSystem.Services.InventoryServices;
using InventoryManagementSystem.Services;
using System.Collections.Generic;

namespace InventoryManagementSystem.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IInventoryAccessService _accessService;
    private readonly ICustomFieldService _customFieldService;
    private readonly IItemService _itemService;
    private readonly IInventoryAdminService _inventoryAdminService;

    public InventoryController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IInventoryAccessService accessService,
        ICustomFieldService customFieldService,
        IItemService itemService,
        IInventoryAdminService inventoryAdminService)
    {
        _context = context;
        _userManager = userManager;
        _accessService = accessService;
        _customFieldService = customFieldService;
        _itemService = itemService;
        _inventoryAdminService = inventoryAdminService;
    }

    private IActionResult HandleServiceResult<T>(ServiceResult<T> result)
    {
        switch (result.ErrorType)
        {
            case ServiceErrorType.None:
                return Ok(result.Data);
            case ServiceErrorType.NotFound:
                return NotFound(new { message = result.ErrorMessage });
            case ServiceErrorType.InvalidInput:
                if (result.ValidationErrors != null)
                {
                    return BadRequest(result.ValidationErrors);
                }
                return BadRequest(new { message = result.ErrorMessage });
            case ServiceErrorType.Forbidden:
                return Forbid();
            case ServiceErrorType.Concurrency:
                return Conflict(new { message = result.ErrorMessage });
            default:
                return StatusCode(500, new { message = result.ErrorMessage ?? "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("Name", "The Inventory Name field is required.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (userId == null) { return Challenge(); }

        if (ModelState.IsValid)
        {
            var inventory = new Inventory { Name = name, OwnerId = userId };
            _context.Add(inventory);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        ViewData["SubmittedName"] = name;
        return View();
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/fields")]
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

    [HttpGet]
    [Route("api/inventory/{inventoryId}/fields")]
    public async Task<IActionResult> GetCustomFields(string inventoryId)
    {
        var result = await _customFieldService.GetCustomFieldsAsync(inventoryId, User);
        return HandleServiceResult(result);
    }

    [HttpPut]
    [Route("api/inventory/fields/{fieldId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCustomField(string fieldId, [FromBody] UpdateFieldRequest fieldUpdate)
    {
        var result = await _customFieldService.UpdateCustomFieldAsync(fieldId, fieldUpdate, User);
        return HandleServiceResult(result);
    }

    [HttpPost]
    [Route("api/inventory/fields/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomFields([FromBody] FieldDeleteRequest deleteRequest)
    {
        var result = await _customFieldService.DeleteCustomFieldsAsync(deleteRequest, User);
        return HandleServiceResult(result);
    }

    [HttpPut]
    [Route("api/inventory/{inventoryId}/fields/reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderCustomFields(string inventoryId, [FromBody] FieldReorderRequest reorderRequest)
    {
        var result = await _customFieldService.ReorderCustomFieldsAsync(inventoryId, reorderRequest, User);
        return HandleServiceResult(result);
    }

    [HttpGet]
    [Route("api/inventory/{inventoryId}/id-format")]
    public async Task<IActionResult> GetIdFormat(string inventoryId)
    {
        var result = await _inventoryAdminService.GetIdFormatAsync(inventoryId, User);
        if (!result.IsSuccess)
        {
            return HandleServiceResult(result);
        }
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var jsonString = JsonSerializer.Serialize((IEnumerable<object>)result.Data!, options);
        return Content(jsonString, "application/json");
    }

    [HttpPut]
    [Route("api/inventory/{inventoryId}/id-format")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIdFormat(string inventoryId, [FromBody] JsonElement format)
    {
        var result = await _inventoryAdminService.SaveIdFormatAsync(inventoryId, format, User);
        return HandleServiceResult(result);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("api/inventory/{inventoryId}/schema")]
    public async Task<IActionResult> GetInventorySchema(string inventoryId)
    {
        var result = await _itemService.GetInventorySchemaAsync(inventoryId);
        return HandleServiceResult(result);
    }

    [HttpPost]
    [AllowAnonymous]
    [Route("api/inventory/{inventoryId}/items-data")]
    public async Task<IActionResult> LoadItemsForDataTable(string inventoryId, [FromForm] DataTablesRequest request)
    {
        var result = await _itemService.GetItemsForDataTableAsync(inventoryId, request);
        return HandleServiceResult(result);
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/regenerate-id")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateId(string inventoryId, [FromBody] RegenerateIdRequest request)
    {
        var result = await _itemService.RegenerateIdAsync(inventoryId, request.InventoryVersion, User);
        if (!result.IsSuccess)
        {
            return HandleServiceResult(result);
        }

        return Ok(new { id = result.Data.Id, boundaries = result.Data.Boundaries });
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/items")]
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

    [HttpPost]
    [Route("api/inventory/items/{itemId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(string itemId, [FromBody] ItemApiRequest request)
    {
        var result = await _itemService.UpdateItemAsync(itemId, request, User);
        return HandleServiceResult(result);
    }

    [HttpPost]
    [Route("api/inventory/items/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItems([FromBody] string[] itemIds)
    {
        var result = await _itemService.DeleteItemsAsync(itemIds, User);
        return HandleServiceResult(result);
    }

    [HttpPut]
    [Route("api/inventory/{inventoryId}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameInventory(string inventoryId, [FromBody] RenameInventoryRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _inventoryAdminService.RenameInventoryAsync(inventoryId, request, User);
        return HandleServiceResult(result);
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/transfer")]
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

    [HttpDelete]
    [Route("api/inventory/{inventoryId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInventory(string inventoryId)
    {
        var result = await _inventoryAdminService.DeleteInventoryAsync(inventoryId, User);
        return HandleServiceResult(result);
    }

    [AllowAnonymous]
    [HttpGet("Inventory/{id:guid}")]
    public async Task<IActionResult> Index(string id)
    {
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == id);
        if (inventory == null) return NotFound();

        var currentUserId = _userManager.GetUserId(User);
        bool isAdmin = User.IsInRole("Admin");
        bool canManageSettings = !string.IsNullOrEmpty(currentUserId) && _accessService.CanManageSettings(inventory, currentUserId, isAdmin);

        if (canManageSettings)
        {
            ViewData["CanManageSettings"] = true;
            ViewData["CanWrite"] = true;
            ViewData["CurrentUserId"] = currentUserId;
            return View("Manage", inventory);
        }
        else
        {
            bool canWriteItems = !string.IsNullOrEmpty(currentUserId) && await _accessService.CanWrite(inventory, currentUserId, isAdmin);
            ViewData["CanManageSettings"] = false;
            ViewData["CanWrite"] = canWriteItems;
            ViewData["CurrentUserId"] = currentUserId;
            return View("View", inventory);
        }
    }

    [NonAction]
    public IActionResult Manage(string id) => NotFound();

    [NonAction]
    public new IActionResult View(string id) => NotFound();
}