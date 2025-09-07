using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryManagementSystem.Services;
using System.Text.Json;
using InventoryManagementSystem.ViewModels;
using InventoryManagementSystem.Services.InventoryServices;

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
        var (field, error) = await _customFieldService.AddCustomFieldAsync(inventoryId, newField, User);
        if (error != null) return BadRequest(error);
        return CreatedAtAction(nameof(GetCustomFields), new { inventoryId }, field);
    }

    [HttpGet]
    [Route("api/inventory/{inventoryId}/fields")]
    public async Task<IActionResult> GetCustomFields(string inventoryId)
    {
        var (fields, error) = await _customFieldService.GetCustomFieldsAsync(inventoryId, User);
        if (error != null) return StatusCode(403, error);
        return Ok(fields);
    }

    [HttpPut]
    [Route("api/inventory/fields/{fieldId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCustomField(string fieldId, [FromBody] CustomFieldDto fieldUpdate)
    {
        var error = await _customFieldService.UpdateCustomFieldAsync(fieldId, fieldUpdate, User);
        if (error != null) return BadRequest(error);
        return Ok();
    }

    [HttpPost]
    [Route("api/inventory/fields/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomFields([FromBody] string[] fieldIds)
    {
        var error = await _customFieldService.DeleteCustomFieldsAsync(fieldIds, User);
        if (error != null) return BadRequest(error);
        return Ok();
    }

    [HttpPut]
    [Route("api/inventory/{inventoryId}/fields/reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderCustomFields(string inventoryId, [FromBody] string[] orderedFieldIds)
    {
        var error = await _customFieldService.ReorderCustomFieldsAsync(inventoryId, orderedFieldIds, User);
        if (error != null) return BadRequest(error);
        return Ok();
    }

    [HttpGet]
    [Route("api/inventory/{inventoryId}/id-format")]
    public async Task<IActionResult> GetIdFormat(string inventoryId)
    {
        var (segments, error) = await _inventoryAdminService.GetIdFormatAsync(inventoryId, User);
        if (error != null) return StatusCode(403, error); 
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return Content(JsonSerializer.Serialize((IEnumerable<object>)segments!, options), "application/json");
    }

    [HttpPut]
    [Route("api/inventory/{inventoryId}/id-format")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIdFormat(string inventoryId, [FromBody] JsonElement format)
    {
        var error = await _inventoryAdminService.SaveIdFormatAsync(inventoryId, format, User);
        if (error != null) return BadRequest(error);
        return Ok();
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("api/inventory/{inventoryId}/items-data")]
    public async Task<IActionResult> GetItemsData(string inventoryId)
    {
        var (data, error) = await _itemService.GetItemsDataAsync(inventoryId);
        if (error != null) return NotFound(error);
        return Ok(data);
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(string inventoryId, [FromBody] ItemApiRequest request)
    {
        var (item, error) = await _itemService.CreateItemAsync(inventoryId, request, User);
        if (error != null) return BadRequest(error);
        return CreatedAtAction(nameof(GetItemsData), new { inventoryId }, item);
    }

    [HttpPut]
    [Route("api/inventory/items/{itemId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(string itemId, [FromBody] ItemApiRequest request)
    {
        var (updatedItem, error) = await _itemService.UpdateItemAsync(itemId, request, User);
        if (error != null) return BadRequest(error);
        return Ok(updatedItem);
    }

    [HttpPost]
    [Route("api/inventory/items/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItems([FromBody] string[] itemIds)
    {
        var error = await _itemService.DeleteItemsAsync(itemIds, User);
        if (error != null) return BadRequest(error);
        return Ok();
    }

    [HttpPut]
    [Route("api/inventory/{inventoryId}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameInventory(string inventoryId, [FromBody] RenameInventoryRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var (newName, error) = await _inventoryAdminService.RenameInventoryAsync(inventoryId, request, User);
        if (error != null) return BadRequest(error);
        return Ok(new { newName });
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/transfer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferOwnership(string inventoryId, [FromBody] TransferOwnershipRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var (message, shouldRedirect, error) = await _inventoryAdminService.TransferOwnershipAsync(inventoryId, request, User);
        if (error != null) return BadRequest(error);
        return Ok(new { message, shouldRedirect });
    }

    [HttpDelete]
    [Route("api/inventory/{inventoryId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInventory(string inventoryId)
    {
        var error = await _inventoryAdminService.DeleteInventoryAsync(inventoryId, User);
        if (error != null) return BadRequest(error);
        return Ok(new { message = "Inventory and all its data have been permanently deleted." });
    }

    [AllowAnonymous]
    [HttpGet("Inventory/{id:guid}")]
    public async Task<IActionResult> Index(string id)
    {
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == id);
        if (inventory == null) return NotFound();

        var currentUserId = _userManager.GetUserId(User);
        bool isOwnerOrAdmin = !string.IsNullOrEmpty(currentUserId) && _accessService.CanManageSettings(inventory, currentUserId, User.IsInRole("Admin"));

        if (isOwnerOrAdmin)
        {
            ViewData["CanManageSettings"] = true;
            ViewData["CanWrite"] = true;
            ViewData["CurrentUserId"] = currentUserId;
            return View("Manage", inventory);
        }
        else
        {
            bool canWrite = !string.IsNullOrEmpty(currentUserId) && await _accessService.CanWrite(inventory, currentUserId, User.IsInRole("Admin"));
            ViewData["CanWrite"] = canWrite;
            return View("View", inventory);
        }
    }

    [NonAction]
    public IActionResult Manage(string id) => NotFound();

    [NonAction]
    public new IActionResult View(string id) => NotFound();
}