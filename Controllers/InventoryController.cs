using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.Models.CustomId;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace InventoryManagementSystem.Controllers;

// DTO for API communication
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
    // Key is TargetColumn (e.g., "CustomString1"), Value is the data
    public Dictionary<string, object?> Fields { get; set; } = new();
}

public class ItemApiRequest
{
    // Key is CustomField.Id, Value is the data
    public Dictionary<string, object> FieldValues { get; set; } = new();
}

[Authorize]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InventoryController> _logger;
    private readonly ICustomIdService _customIdService;

    public InventoryController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<InventoryController> logger,
        ICustomIdService customIdService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _customIdService = customIdService;
    }

    // GET: /Inventory/Create
    public IActionResult Create()
    {
        return View();
    }

    // GET: /Inventory/Manage/{id}
    [HttpGet]
    public async Task<IActionResult> Manage(string id)
    {
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.Id == id);

        if (inventory == null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            // If the user is not the owner and not an Admin, deny access.
            return Forbid();
        }

        return View(inventory);
    }

    // POST: /Inventory/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("Name", "The Inventory Name field is required.");
        }

        var userId = _userManager.GetUserId(User);
        if (userId == null) { return Challenge(); }

        if (ModelState.IsValid)
        {
            var inventory = new Inventory
            {
                Name = name,
                OwnerId = userId
            };

            _context.Add(inventory);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        // If we got this far, something failed, redisplay form
        ViewData["SubmittedName"] = name;
        return View();
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/fields")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomField(string inventoryId, [FromBody] CustomFieldDto newField)
    {
        var inventory = await _context.Inventories.FindAsync(inventoryId);
        if (inventory == null) return NotFound();

        // Authorization check
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        // Basic validation
        if (string.IsNullOrWhiteSpace(newField.Name) || !Enum.TryParse<CustomFieldType>(newField.Type, out var fieldType))
        {
            return BadRequest("Invalid field name or type.");
        }

        var existingFields = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId && cf.Type == fieldType)
            .ToListAsync();

        const int maxFieldsPerType = 3;
        if (existingFields.Count >= maxFieldsPerType)
        {
            return BadRequest($"Cannot add another field of type '{fieldType}'. Maximum of {maxFieldsPerType} reached.");
        }

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

        if (string.IsNullOrEmpty(targetColumn))
        {
            return BadRequest("Could not find an available column for this field type.");
        }

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
        }
        catch (DbUpdateException ex)
        {
            // Check for unique constraint violation (race condition)
            if (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                return Conflict("A field with the same properties was created simultaneously. Please try again.");
            }

            // Catch other potential DB errors, like a string being too long
            _logger.LogError(ex, "Database error while adding custom field for inventory {InventoryId}", inventoryId);
            return BadRequest("A database error occurred. Please check your input (e.g., field name is not too long).");
        }

        // Return the created field (with its new ID) so the UI can update
        var resultDto = new CustomFieldDto
        {
            Id = customField.Id,
            Name = customField.Name,
            Type = customField.Type.ToString()
        };

        return CreatedAtAction(nameof(GetCustomFields), new { inventoryId }, resultDto);
    }

    [HttpGet]
    [Route("api/inventory/{inventoryId}/fields")]
    public async Task<IActionResult> GetCustomFields(string inventoryId)
    {
        var inventory = await _context.Inventories.FindAsync(inventoryId);
        if (inventory == null) return NotFound();

        // Authorization check
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId)
            .OrderBy(cf => cf.Order)
            .Select(cf => new CustomFieldDto
            {
                Id = cf.Id,
                Name = cf.Name,
                Type = cf.Type.ToString()
            })
            .ToListAsync();

        return Ok(fields);
    }

    [HttpPut]
    [Route("api/inventory/fields/{fieldId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCustomField(string fieldId, [FromBody] CustomFieldDto fieldUpdate)
    {
        var fieldToUpdate = await _context.CustomFields
            .Include(f => f.Inventory)
            .FirstOrDefaultAsync(f => f.Id == fieldId);

        if (fieldToUpdate == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (fieldToUpdate.Inventory?.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(fieldUpdate.Name))
        {
            return BadRequest("Field name cannot be empty.");
        }

        fieldToUpdate.Name = fieldUpdate.Name;
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPost]
    [Route("api/inventory/fields/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomFields([FromBody] string[] fieldIds)
    {
        if (fieldIds == null || !fieldIds.Any())
        {
            return BadRequest("No field IDs provided.");
        }

        var fieldsToDelete = await _context.CustomFields
            .Where(cf => fieldIds.Contains(cf.Id))
            .Include(cf => cf.Inventory)
            .ToListAsync();

        if (!fieldsToDelete.Any()) return Ok();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var inventories = fieldsToDelete.Select(f => f.Inventory).Distinct();
        if (inventories.Any(inv => inv?.OwnerId != currentUserId && !User.IsInRole("Admin")))
        {
            return Forbid();
        }

        if (fieldsToDelete.Select(f => f.InventoryId).Distinct().Count() > 1)
        {
            return BadRequest("Cannot delete fields from multiple inventories in a single request.");
        }

        // Clear the orphaned data in the Items table before deleting the fields.
        foreach (var field in fieldsToDelete)
        {
            var itemsToUpdate = await _context.Items
                .Where(i => i.InventoryId == field.InventoryId)
                .ToListAsync();

            foreach (var item in itemsToUpdate)
            {
                var propInfo = typeof(Item).GetProperty(field.TargetColumn);
                propInfo?.SetValue(item, null);
            }
        }

        _context.CustomFields.RemoveRange(fieldsToDelete);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPut]
    [Route("api/inventory/{inventoryId}/fields/reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderCustomFields(string inventoryId, [FromBody] string[] orderedFieldIds)
    {
        var inventory = await _context.Inventories.FindAsync(inventoryId);
        if (inventory == null) return NotFound();

        // Authorization check
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var fieldsToUpdate = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId)
            .ToListAsync();

        if (fieldsToUpdate.Count != orderedFieldIds.Length || fieldsToUpdate.Any(f => !orderedFieldIds.Contains(f.Id)))
        {
            return BadRequest("The provided list of field IDs is incomplete or contains invalid IDs for this inventory.");
        }

        for (int i = 0; i < orderedFieldIds.Length; i++)
        {
            var fieldId = orderedFieldIds[i];
            var field = fieldsToUpdate.FirstOrDefault(f => f.Id == fieldId);
            if (field != null)
            {
                field.Order = i;
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet]
    [Route("api/inventory/{inventoryId}/items-data")]
    public async Task<IActionResult> GetItemsData(string inventoryId)
    {
        var inventory = await _context.Inventories.FindAsync(inventoryId);
        if (inventory == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var fields = await _context.CustomFields
        .Where(cf => cf.InventoryId == inventoryId)
        .OrderBy(cf => cf.Order)
        .AsNoTracking()
        .Select(f => new // Project into an anonymous object or a DTO
        {
            f.Id,
            f.Name,
            f.Order,
            f.TargetColumn,
            Type = f.Type.ToString() // This is the critical fix
        })
        .ToListAsync();

        var items = await _context.Items
            .Where(i => i.InventoryId == inventoryId)
            .AsNoTracking()
            .ToListAsync();

        var itemDtos = items.Select(item => new ItemDto
        {
            Id = item.Id,
            CustomId = item.CustomId,
            CreatedAt = item.CreatedAt,
            Fields = fields.ToDictionary(
                field => field.TargetColumn,
                field => typeof(Item).GetProperty(field.TargetColumn)?.GetValue(item)
            )
        }).ToList();

        return Ok(new { fields, items = itemDtos });
    }

    [HttpPost]
    [Route("api/inventory/{inventoryId}/items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(string inventoryId, [FromBody] ItemApiRequest request)
    {
        // Use a transaction to ensure Item creation and Inventory sequence update are atomic.
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Lock the inventory row to prevent race conditions on the sequence counter.
        var inventory = await _context.Inventories
            .FromSql($"SELECT * FROM \"Inventories\" WHERE \"Id\" = {inventoryId} FOR UPDATE")
            .FirstOrDefaultAsync();

        if (inventory == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId)
            .AsNoTracking()
            .ToListAsync();

        var newItem = new Item { InventoryId = inventoryId };
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

                        if (string.IsNullOrEmpty(valueStr))
                        {
                            convertedValue = null;
                        }
                        else if (targetType == typeof(bool))
                        {
                            convertedValue = valueStr.Equals("true", StringComparison.OrdinalIgnoreCase) || valueStr.Equals("on", StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            convertedValue = Convert.ChangeType(valueStr, targetType, CultureInfo.InvariantCulture);
                        }
                        propInfo.SetValue(newItem, convertedValue);
                    }
                    catch (Exception ex) when (ex is FormatException || ex is InvalidCastException)
                    {
                        validationErrors[field.Id] = $"Invalid value for '{field.Name}'. Please check the format.";
                    }
                }
            }
        }

        if (validationErrors.Any())
        {
            return BadRequest(new { message = "Validation failed.", errors = validationErrors });
        }

        // Generate Custom ID if format is defined
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

        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _context.Entry(newItem).State = EntityState.Detached;
                var savedItem = await _context.Items.FindAsync(newItem.Id);

                if (savedItem == null)
                {
                    return StatusCode(500, "Failed to retrieve the saved item.");
                }

                var createdItemDto = new ItemDto
                {
                    Id = savedItem.Id,
                    CustomId = savedItem.CustomId,
                    CreatedAt = savedItem.CreatedAt,
                    Fields = fields.ToDictionary(
                        field => field.TargetColumn,
                        field => typeof(Item).GetProperty(field.TargetColumn)?.GetValue(savedItem)
                    )
                };
                return CreatedAtAction(nameof(GetItemsData), new { inventoryId }, createdItemDto);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                _logger.LogWarning(ex, "Unique constraint violation on CustomId for inventory {InventoryId}. This should be rare with the new sequence strategy. Retry {RetryCount}/{MaxRetries}", inventoryId, i + 1, maxRetries);

                _context.Entry(newItem).State = EntityState.Detached;
                _context.Entry(inventory).State = EntityState.Detached;

                if (i == maxRetries - 1)
                {
                    await transaction.RollbackAsync();
                    return Conflict("Failed to generate a unique item ID after multiple attempts. Please try again.");
                }

                inventory = await _context.Inventories
                    .FromSql($"SELECT * FROM \"Inventories\" WHERE \"Id\" = {inventoryId} FOR UPDATE")
                    .FirstAsync();
                newItem.Id = Guid.NewGuid().ToString();
                if (!string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
                {
                    var segments = JsonSerializer.Deserialize<List<IdSegment>>(inventory.CustomIdFormat);
                    newItem.CustomId = _customIdService.GenerateId(inventory, segments!);
                }

                _context.Items.Add(newItem);
            }
        }

        await transaction.RollbackAsync();
        return StatusCode(500, "An unexpected error occurred while creating the item.");
    }

    [HttpGet]
    [Route("api/inventory/items/{itemId}")]
    public async Task<IActionResult> GetItem(string itemId)
    {
        var item = await _context.Items
            .Include(i => i.Inventory)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (item.Inventory?.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == item.InventoryId)
            .OrderBy(cf => cf.Order)
            .AsNoTracking().ToListAsync();

        var itemDto = new ItemDto
        {
            Id = item.Id,
            CreatedAt = item.CreatedAt,
            Fields = fields.ToDictionary(
                field => field.TargetColumn,
                field => typeof(Item).GetProperty(field.TargetColumn)?.GetValue(item)
            )
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

            var itemToUpdate = await _context.Items
                .Include(it => it.Inventory)
                .FirstOrDefaultAsync(it => it.Id == itemId);

            if (itemToUpdate == null) return NotFound();

            var inventory = await _context.Inventories
                .FromSql($"SELECT * FROM \"Inventories\" WHERE \"Id\" = {itemToUpdate.InventoryId} FOR UPDATE")
                .FirstAsync();
            itemToUpdate.Inventory = inventory;

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (itemToUpdate.Inventory?.OwnerId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var fields = await _context.CustomFields
                .Where(cf => cf.InventoryId == itemToUpdate.InventoryId)
                .AsNoTracking()
                .ToListAsync();

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

            if (validationErrors.Any()) { return BadRequest(validationErrors); }

            if (itemToUpdate.Inventory != null && !string.IsNullOrWhiteSpace(itemToUpdate.Inventory.CustomIdFormat))
            {
                bool needsNewId = string.IsNullOrEmpty(itemToUpdate.CustomId) ||
                                  itemToUpdate.CustomIdFormatHashApplied != itemToUpdate.Inventory.CustomIdFormatHash;
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
                return Ok(); // On success, exit the loop and method.
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Unique constraint violation on CustomId during UPDATE. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);

                // Detach the failed entities to avoid tracking conflicts on the next loop iteration.
                _context.Entry(itemToUpdate).State = EntityState.Detached;
                _context.Entry(inventory).State = EntityState.Detached;

                if (i == maxRetries - 1)
                {
                    return Conflict("Failed to generate a unique item ID after multiple attempts. The inventory may be experiencing high traffic. Please try again.");
                }
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

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var authorizedItemCount = await _context.Items
    .CountAsync(i => itemIds.Contains(i.Id) && (i.Inventory != null && i.Inventory.OwnerId == currentUserId || User.IsInRole("Admin")));

        if (authorizedItemCount != itemIds.Length)
        {
            return Forbid();
        }

        var itemsToDelete = await _context.Items
            .Where(i => itemIds.Contains(i.Id))
            .ToListAsync();

        _context.Items.RemoveRange(itemsToDelete);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet]
    [Route("api/inventory/{inventoryId}/id-format")]
    public async Task<IActionResult> GetIdFormat(string inventoryId)
    {
        var inventory = await _context.Inventories.FindAsync(inventoryId);
        if (inventory == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
        {
            return Content("[]", "application/json");
        }

        try
        {
            var segments = JsonIdSegmentDeserializer.Deserialize(inventory.CustomIdFormat);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
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
        var inventory = await _context.Inventories.FindAsync(inventoryId);
        if (inventory == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

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
                // If there are no segments, clear the format and hash.
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
        return Ok();
    }
}