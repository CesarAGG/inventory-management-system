using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Npgsql;

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

    public InventoryController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<InventoryController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
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

    [HttpDelete]
    [Route("api/inventory/fields/{fieldId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomField(string fieldId)
    {
        var field = await _context.CustomFields.Include(cf => cf.Inventory)
            .FirstOrDefaultAsync(cf => cf.Id == fieldId);

        if (field == null) return NotFound();

        // Authorization check
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (field.Inventory?.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        _context.CustomFields.Remove(field);
        await _context.SaveChangesAsync();

        return NoContent(); // Success, no content to return
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
            .ToListAsync();

        var items = await _context.Items
            .Where(i => i.InventoryId == inventoryId)
            .AsNoTracking()
            .ToListAsync();

        var itemDtos = items.Select(item => new ItemDto
        {
            Id = item.Id,
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
        var inventory = await _context.Inventories.FindAsync(inventoryId);
        if (inventory == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId)
            .ToListAsync();

        var newItem = new Item { InventoryId = inventoryId };

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

                        if (targetType == typeof(bool))
                        {
                            convertedValue = valueStr != null && (valueStr.Equals("true", StringComparison.OrdinalIgnoreCase) || valueStr.Equals("on", StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            convertedValue = Convert.ChangeType(valueStr, targetType, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        propInfo.SetValue(newItem, convertedValue);
                    }
                    catch (Exception)
                    {
                        // Silently ignore fields that fail conversion
                    }
                }
            }
        }
        _context.Items.Add(newItem);
        await _context.SaveChangesAsync();
        var createdItemDto = new ItemDto
        {
            Id = newItem.Id,
            Fields = fields.ToDictionary(
                field => field.TargetColumn,
                field => typeof(Item).GetProperty(field.TargetColumn)?.GetValue(newItem)
            )
        };
        return CreatedAtAction(nameof(GetItemsData), new { inventoryId }, createdItemDto);
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
}