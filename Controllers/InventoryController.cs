using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryManagementSystem.Services;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.Controllers;

public class CustomFieldDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class ItemDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object?> Fields { get; set; } = new();
}

public class ItemApiRequest
{
    public Dictionary<string, object> FieldValues { get; set; } = new();
}

public class RenameInventoryRequest
{
    [Required]
    [StringLength(100, ErrorMessage = "The inventory name cannot exceed 100 characters.")]
    public string NewName { get; set; } = string.Empty;
}

public class TransferOwnershipRequest
{
    [Required]
    [EmailAddress]
    public string NewOwnerEmail { get; set; } = string.Empty;
}

[Authorize]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InventoryController> _logger;
    private readonly ICustomIdService _customIdService;
    private readonly IInventoryAccessService _accessService;

    public InventoryController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<InventoryController> logger,
        ICustomIdService customIdService,
        IInventoryAccessService accessService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _customIdService = customIdService;
        _accessService = accessService;
    }

    private string GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin() => User.IsInRole("Admin");

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

        var userId = GetCurrentUserId();
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
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        if (string.IsNullOrWhiteSpace(newField.Name) || !Enum.TryParse<CustomFieldType>(newField.Type, out var fieldType))
            return BadRequest("Invalid field name or type.");

        var existingFields = await _context.CustomFields.Where(cf => cf.InventoryId == inventoryId && cf.Type == fieldType).ToListAsync();
        const int maxFieldsPerType = 3;
        if (existingFields.Count >= maxFieldsPerType)
            return BadRequest($"Cannot add another field of type '{fieldType}'. Maximum of {maxFieldsPerType} reached.");

        string targetColumn = "";
        for (int i = 1; i <= maxFieldsPerType; i++)
        {
            var columnName = $"Custom{fieldType}{i}";
            if (!existingFields.Any(f => f.TargetColumn == columnName))
            {
                targetColumn = columnName;
                break;
            }
        }

        if (string.IsNullOrEmpty(targetColumn)) return BadRequest("Could not find an available column for this field type.");

        var customField = new CustomField
        {
            InventoryId = inventoryId,
            Name = newField.Name,
            Type = fieldType,
            TargetColumn = targetColumn,
            Order = (existingFields.Max(f => (int?)f.Order) ?? -1) + 1
        };
        _context.CustomFields.Add(customField);

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                return Conflict("A field with the same properties was created simultaneously. Please try again.");
            _logger.LogError(ex, "Database error while adding custom field for inventory {InventoryId}", inventoryId);
            return BadRequest("A database error occurred. Please check your input (e.g., field name is not too long).");
        }

        var resultDto = new CustomFieldDto { Id = customField.Id, Name = customField.Name, Type = customField.Type.ToString() };
        return CreatedAtAction(nameof(GetCustomFields), new { inventoryId }, resultDto);
    }

    [HttpGet]
    [Route("api/inventory/{inventoryId}/fields")]
    public async Task<IActionResult> GetCustomFields(string inventoryId)
    {
        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId).OrderBy(cf => cf.Order)
            .Select(cf => new CustomFieldDto { Id = cf.Id, Name = cf.Name, Type = cf.Type.ToString() }).ToListAsync();
        return Ok(fields);
    }

    [HttpPut]
    [Route("api/inventory/fields/{fieldId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCustomField(string fieldId, [FromBody] CustomFieldDto fieldUpdate)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var fieldToUpdate = await _context.CustomFields.Include(f => f.Inventory).FirstOrDefaultAsync(f => f.Id == fieldId);
        if (fieldToUpdate == null || fieldToUpdate.Inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(fieldToUpdate.Inventory, GetCurrentUserId(), IsAdmin())) return Forbid();
        if (string.IsNullOrWhiteSpace(fieldUpdate.Name)) return BadRequest("Field name cannot be empty.");

        fieldToUpdate.Name = fieldUpdate.Name;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok();
    }

    [HttpPost]
    [Route("api/inventory/fields/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomFields([FromBody] string[] fieldIds)
    {
        if (fieldIds == null || !fieldIds.Any()) return BadRequest("No field IDs provided.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var fieldsToDelete = await _context.CustomFields.Where(cf => fieldIds.Contains(cf.Id)).Include(cf => cf.Inventory).ToListAsync();
        if (!fieldsToDelete.Any()) return Ok();

        var inventory = fieldsToDelete.First().Inventory;
        if (inventory == null || fieldsToDelete.Any(f => f.InventoryId != inventory.Id))
            return BadRequest("All fields must belong to the same inventory.");

        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        foreach (var field in fieldsToDelete)
        {
            var itemsToUpdate = await _context.Items.Where(i => i.InventoryId == field.InventoryId).ToListAsync();
            foreach (var item in itemsToUpdate)
            {
                var propInfo = typeof(Item).GetProperty(field.TargetColumn);
                propInfo?.SetValue(item, null);
            }
        }
        _context.CustomFields.RemoveRange(fieldsToDelete);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok();
    }

    [HttpPut]
    [Route("api/inventory/{inventoryId}/fields/reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderCustomFields(string inventoryId, [FromBody] string[] orderedFieldIds)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        var fieldsToUpdate = await _context.CustomFields.Where(cf => cf.InventoryId == inventoryId).ToListAsync();
        if (fieldsToUpdate.Count != orderedFieldIds.Length || fieldsToUpdate.Any(f => !orderedFieldIds.Contains(f.Id)))
            return BadRequest("The provided list of field IDs is incomplete or contains invalid IDs for this inventory.");

        for (int i = 0; i < orderedFieldIds.Length; i++)
        {
            var fieldId = orderedFieldIds[i];
            var field = fieldsToUpdate.FirstOrDefault(f => f.Id == fieldId);
            if (field != null) field.Order = i;
        }
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok();
    }

    [HttpGet]
    [Route("api/inventory/{inventoryId}/id-format")]
    public async Task<IActionResult> GetIdFormat(string inventoryId)
    {
        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        if (string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
            return Content("[]", "application/json");

        try
        {
            var segments = JsonIdSegmentDeserializer.Deserialize(inventory.CustomIdFormat);
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var jsonString = JsonSerializer.Serialize((IEnumerable<object>)segments, options);
            return Content(jsonString, "application/json");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse stored CustomIdFormat for inventory {InventoryId}", inventoryId);
            return StatusCode(500, "The stored ID format is corrupted.");
        }
    }

    [HttpPut]
    [Route("api/inventory/{inventoryId}/id-format")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIdFormat(string inventoryId, [FromBody] JsonElement format)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        var jsonString = format.ToString();
        try
        {
            var segments = JsonIdSegmentDeserializer.Deserialize(jsonString);
            if (segments.Any())
            {
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var canonicalFormatString = JsonSerializer.Serialize((IEnumerable<object>)segments, options);
                inventory.CustomIdFormat = canonicalFormatString;
                inventory.CustomIdFormatHash = HashingHelper.ComputeSha256Hash(canonicalFormatString);
            }
            else
            {
                inventory.CustomIdFormat = null;
                inventory.CustomIdFormatHash = null;
                inventory.LastSequenceValue = 0;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse and save CustomIdFormat for inventory {InventoryId}", inventoryId);
            return BadRequest("The provided ID format was malformed.");
        }
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok();
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("api/inventory/{inventoryId}/items-data")]
    public async Task<IActionResult> GetItemsData(string inventoryId)
    {
        var inventoryExists = await _context.Inventories.AnyAsync(i => i.Id == inventoryId);
        if (!inventoryExists)
        {
            return NotFound();
        }

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId).OrderBy(cf => cf.Order).AsNoTracking()
            .Select(f => new { f.Id, f.Name, f.Order, f.TargetColumn, Type = f.Type.ToString() }).ToListAsync();

        var items = await _context.Items.Where(i => i.InventoryId == inventoryId).AsNoTracking().ToListAsync();
        var itemDtos = items.Select(item => new ItemDto
        {
            Id = item.Id,
            CustomId = item.CustomId,
            CreatedAt = item.CreatedAt,
            Fields = fields.ToDictionary(field => field.TargetColumn, field => typeof(Item).GetProperty(field.TargetColumn)?.GetValue(item))
        }).ToList();

        return Ok(new { fields, items = itemDtos });
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(string inventoryId, [FromBody] ItemApiRequest request)
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var inventory = await _context.Inventories.FirstOrDefaultAsync(inv => inv.Id == inventoryId);
            if (inventory == null) { await transaction.RollbackAsync(); return NotFound(); }
            if (!await _accessService.CanWrite(inventory, GetCurrentUserId(), IsAdmin())) { await transaction.RollbackAsync(); return Forbid(); }

            var fields = await _context.CustomFields.Where(cf => cf.InventoryId == inventoryId).AsNoTracking().ToListAsync();
            var newItem = new Item { Id = Guid.NewGuid().ToString(), InventoryId = inventoryId };
            var validationErrors = new Dictionary<string, string>();

            foreach (var field in fields)
            {
                if (request.FieldValues.TryGetValue(field.Id, out var value) && value != null)
                {
                    var propInfo = typeof(Item).GetProperty(field.TargetColumn);
                    if (propInfo != null)
                    {
                        try
                        {
                            var valueStr = value.ToString();
                            object? convertedValue;
                            var targetType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;
                            if (string.IsNullOrEmpty(valueStr)) { convertedValue = null; }
                            else if (targetType == typeof(bool)) { convertedValue = valueStr.Equals("true", StringComparison.OrdinalIgnoreCase) || valueStr.Equals("on", StringComparison.OrdinalIgnoreCase); }
                            else { convertedValue = Convert.ChangeType(valueStr, targetType, CultureInfo.InvariantCulture); }
                            propInfo.SetValue(newItem, convertedValue);
                        }
                        catch (Exception ex) when (ex is FormatException || ex is InvalidCastException)
                        {
                            validationErrors[field.Id] = $"Invalid value for '{field.Name}'.";
                        }
                    }
                }
            }
            if (validationErrors.Any()) { await transaction.RollbackAsync(); return BadRequest(new { message = "Validation failed.", errors = validationErrors }); }

            if (!string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
            {
                var segments = JsonIdSegmentDeserializer.Deserialize(inventory.CustomIdFormat);
                if (segments != null && segments.Any())
                {
                    newItem.CustomId = _customIdService.GenerateId(inventory, segments);
                    newItem.CustomIdFormatHashApplied = inventory.CustomIdFormatHash;
                }
            }
            _context.Items.Add(newItem);

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _context.Entry(newItem).State = EntityState.Detached;
                var savedItem = await _context.Items.AsNoTracking().FirstAsync(it => it.Id == newItem.Id);

                var createdItemDto = new ItemDto
                {
                    Id = savedItem.Id,
                    CustomId = savedItem.CustomId,
                    CreatedAt = savedItem.CreatedAt,
                    Fields = fields.ToDictionary(field => field.TargetColumn, field => typeof(Item).GetProperty(field.TargetColumn)?.GetValue(savedItem))
                };
                return CreatedAtAction(nameof(GetItemsData), new { inventoryId }, createdItemDto);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                _logger.LogWarning(ex, "Unique constraint violation on CustomId. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);
                if (i == maxRetries - 1)
                {
                    await transaction.RollbackAsync();
                    return Conflict("Failed to generate a unique item ID after multiple attempts.");
                }
                // The transaction is rolled back by the using block's implicit dispose on exception
            }
        }
        return StatusCode(500, "An unexpected error occurred while creating the item.");
    }

    [AllowAnonymous]
    [HttpGet]
    [Route("api/inventory/items/{itemId}")]
    public async Task<IActionResult> GetItem(string itemId)
    {
        var item = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null) return NotFound();

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == item.InventoryId).OrderBy(cf => cf.Order).AsNoTracking().ToListAsync();
        var itemDto = new ItemDto
        {
            Id = item.Id,
            CustomId = item.CustomId,
            CreatedAt = item.CreatedAt,
            Fields = fields.ToDictionary(field => field.TargetColumn, field => typeof(Item).GetProperty(field.TargetColumn)?.GetValue(item))
        };
        return Ok(itemDto);
    }

    [HttpPut]
    [Route("api/inventory/items/{itemId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(string itemId, [FromBody] ItemApiRequest request)
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var itemToUpdate = await _context.Items.Include(it => it.Inventory).FirstOrDefaultAsync(it => it.Id == itemId);
            if (itemToUpdate == null || itemToUpdate.Inventory == null) { await transaction.RollbackAsync(); return NotFound(); }
            if (!await _accessService.CanWrite(itemToUpdate.Inventory, GetCurrentUserId(), IsAdmin())) { await transaction.RollbackAsync(); return Forbid(); }

            var fields = await _context.CustomFields.Where(cf => cf.InventoryId == itemToUpdate.InventoryId).AsNoTracking().ToListAsync();
            var validationErrors = new Dictionary<string, string>();

            foreach (var field in fields)
            {
                if (request.FieldValues.TryGetValue(field.Id, out var value))
                {
                    var propInfo = typeof(Item).GetProperty(field.TargetColumn);
                    if (propInfo != null)
                    {
                        try
                        {
                            var valueStr = value?.ToString();
                            object? convertedValue;
                            var targetType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;
                            if (string.IsNullOrEmpty(valueStr)) { convertedValue = null; }
                            else if (targetType == typeof(bool)) { convertedValue = valueStr.Equals("true", StringComparison.OrdinalIgnoreCase) || valueStr.Equals("on", StringComparison.OrdinalIgnoreCase); }
                            else { convertedValue = Convert.ChangeType(valueStr, targetType, CultureInfo.InvariantCulture); }
                            propInfo.SetValue(itemToUpdate, convertedValue);
                        }
                        catch (Exception) { validationErrors.Add(field.Id, $"Invalid data format for field '{field.Name}'."); }
                    }
                }
            }
            if (validationErrors.Any()) { await transaction.RollbackAsync(); return BadRequest(validationErrors); }

            if (!string.IsNullOrWhiteSpace(itemToUpdate.Inventory.CustomIdFormat))
            {
                bool needsNewId = string.IsNullOrEmpty(itemToUpdate.CustomId) || itemToUpdate.CustomIdFormatHashApplied != itemToUpdate.Inventory.CustomIdFormatHash;
                if (needsNewId)
                {
                    var segments = JsonIdSegmentDeserializer.Deserialize(itemToUpdate.Inventory.CustomIdFormat);
                    if (segments.Any())
                    {
                        itemToUpdate.CustomId = _customIdService.GenerateId(itemToUpdate.Inventory, segments);
                        itemToUpdate.CustomIdFormatHashApplied = itemToUpdate.Inventory.CustomIdFormatHash;
                    }
                }
            }
            else
            {
                itemToUpdate.CustomId = string.Empty;
                itemToUpdate.CustomIdFormatHashApplied = null;
            }

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                _logger.LogWarning(ex, "Unique constraint violation on CustomId during UPDATE. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);
                if (i == maxRetries - 1)
                {
                    await transaction.RollbackAsync();
                    return Conflict("Failed to generate a unique item ID after multiple attempts.");
                }
                // On exception, the `using` block will dispose and roll back the transaction.
            }
        }
        return StatusCode(500, "An unexpected error occurred while updating the item.");
    }

    [HttpPost]
    [Route("api/inventory/items/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItems([FromBody] string[] itemIds)
    {
        if (itemIds == null || !itemIds.Any()) return BadRequest("No item IDs provided.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var itemsToDelete = await _context.Items.Where(i => itemIds.Contains(i.Id)).Include(i => i.Inventory).ToListAsync();
        if (itemsToDelete.Count != itemIds.Length) return BadRequest("One or more items could not be found.");
        if (!itemsToDelete.Any()) return Ok();

        var inventory = itemsToDelete.First().Inventory;
        if (inventory == null || itemsToDelete.Any(i => i.InventoryId != inventory.Id))
            return BadRequest("All items must belong to the same inventory for a batch delete.");

        if (!await _accessService.CanWrite(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        _context.Items.RemoveRange(itemsToDelete);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok();
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

    [HttpPut]
    [Route("api/inventory/{inventoryId}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameInventory(string inventoryId, [FromBody] RenameInventoryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null)
        {
            return NotFound();
        }

        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin()))
        {
            return Forbid();
        }

        inventory.Name = request.NewName;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new { newName = inventory.Name });
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/transfer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferOwnership(string inventoryId, [FromBody] TransferOwnershipRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null)
        {
            return NotFound(new { message = "Inventory not found." });
        }

        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin()))
        {
            return Forbid();
        }

        var newOwner = await _userManager.FindByEmailAsync(request.NewOwnerEmail);
        if (newOwner == null)
        {
            return BadRequest(new { message = "The specified user does not exist." });
        }

        if (newOwner.Id == inventory.OwnerId)
        {
            return BadRequest(new { message = "This user is already the owner." });
        }

        bool shouldRedirect = !User.IsInRole("Admin");

        var newOwnerPermission = await _context.InventoryUserPermissions
            .FirstOrDefaultAsync(p => p.InventoryId == inventoryId && p.UserId == newOwner.Id);
        if (newOwnerPermission != null)
        {
            _context.InventoryUserPermissions.Remove(newOwnerPermission);
        }
        
        inventory.OwnerId = newOwner.Id;

        var oldOwnerId = inventory.OwnerId;
        var oldOwnerPermission = await _context.InventoryUserPermissions
            .FirstOrDefaultAsync(p => p.InventoryId == inventoryId && p.UserId == oldOwnerId);
        if (oldOwnerPermission != null)
        {
            _context.InventoryUserPermissions.Remove(oldOwnerPermission);
        }

        inventory.OwnerId = newOwner.Id;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = $"Ownership successfully transferred to {newOwner.Email}.",
            shouldRedirect = shouldRedirect
        });
    }

    [HttpDelete]
    [Route("api/inventory/{inventoryId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInventory(string inventoryId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null)
        {
            return NotFound();
        }

        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin()))
        {
            return Forbid();
        }

        _context.Inventories.Remove(inventory);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new { message = "Inventory and all its data have been permanently deleted." });
    }

    [NonAction]
    public IActionResult Manage(string id) => NotFound(); // Prevent direct access

    [NonAction]
    public new IActionResult View(string id) => NotFound(); // Prevent direct access
}