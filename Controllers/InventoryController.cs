using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Security.Claims;

namespace InventoryManagementSystem.Controllers;

// DTO for API communication
public class CustomFieldDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

[Authorize]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public InventoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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
    [ValidateAntiForgeryToken] // CSRF Protection
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

        var customField = new CustomField
        {
            InventoryId = inventoryId,
            Name = newField.Name,
            Type = fieldType
        };

        // Set the order to be the last
        var maxOrder = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId)
            .Select(cf => (int?)cf.Order) // Select as nullable int to handle empty list
            .MaxAsync();

        customField.Order = (maxOrder ?? -1) + 1;

        _context.CustomFields.Add(customField);
        await _context.SaveChangesAsync();

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
}