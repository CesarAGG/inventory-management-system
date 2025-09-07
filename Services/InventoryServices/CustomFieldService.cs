using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services.InventoryServices;

public class CustomFieldService : ICustomFieldService
{
    private readonly ApplicationDbContext _context;
    private readonly IInventoryAccessService _accessService;
    private readonly ILogger<CustomFieldService> _logger;

    public CustomFieldService(ApplicationDbContext context, IInventoryAccessService accessService, ILogger<CustomFieldService> logger)
    {
        _context = context;
        _accessService = accessService;
        _logger = logger;
    }

    private string GetUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("Admin");

    public async Task<(CustomFieldDto? Field, object? Error)> AddCustomFieldAsync(string inventoryId, CustomFieldDto newField, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return (null, new { message = "Inventory not found." });
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return (null, new { message = "Forbidden." });

        if (string.IsNullOrWhiteSpace(newField.Name) || !Enum.TryParse<CustomFieldType>(newField.Type, out var fieldType))
            return (null, new { message = "Invalid field name or type." });

        var existingFields = await _context.CustomFields.Where(cf => cf.InventoryId == inventoryId && cf.Type == fieldType).ToListAsync();
        const int maxFieldsPerType = 3;
        if (existingFields.Count >= maxFieldsPerType)
            return (null, new { message = $"Cannot add another field of type '{fieldType}'. Maximum of {maxFieldsPerType} reached." });

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

        if (string.IsNullOrEmpty(targetColumn)) return (null, new { message = "Could not find an available column for this field type." });

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
                return (null, new { message = "A field with the same properties was created simultaneously. Please try again." });
            _logger.LogError(ex, "Database error while adding custom field for inventory {InventoryId}", inventoryId);
            return (null, new { message = "A database error occurred. Please check your input (e.g., field name is not too long)." });
        }

        var resultDto = new CustomFieldDto { Id = customField.Id, Name = customField.Name, Type = customField.Type.ToString() };
        return (resultDto, null);
    }

    public async Task<(List<CustomFieldDto>? Fields, object? Error)> GetCustomFieldsAsync(string inventoryId, ClaimsPrincipal user)
    {
        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return (null, new { message = "Inventory not found." });
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return (null, new { message = "Forbidden." });

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId).OrderBy(cf => cf.Order)
            .Select(cf => new CustomFieldDto { Id = cf.Id, Name = cf.Name, Type = cf.Type.ToString() }).ToListAsync();
        return (fields, null);
    }

    public async Task<object?> UpdateCustomFieldAsync(string fieldId, CustomFieldDto fieldUpdate, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var fieldToUpdate = await _context.CustomFields.Include(f => f.Inventory).FirstOrDefaultAsync(f => f.Id == fieldId);
        if (fieldToUpdate == null || fieldToUpdate.Inventory == null) return new { message = "Field not found." };
        if (!_accessService.CanManageSettings(fieldToUpdate.Inventory, GetUserId(user), IsAdmin(user))) return new { message = "Forbidden." };
        if (string.IsNullOrWhiteSpace(fieldUpdate.Name)) return new { message = "Field name cannot be empty." };

        fieldToUpdate.Name = fieldUpdate.Name;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return null;
    }

    public async Task<object?> DeleteCustomFieldsAsync(string[] fieldIds, ClaimsPrincipal user)
    {
        if (fieldIds == null || !fieldIds.Any()) return new { message = "No field IDs provided." };

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var fieldsToDelete = await _context.CustomFields
            .Where(cf => fieldIds.Contains(cf.Id))
            .Include(cf => cf.Inventory)
            .ToListAsync();

        if (!fieldsToDelete.Any()) return null;

        var inventory = fieldsToDelete.First().Inventory;
        if (inventory == null || fieldsToDelete.Any(f => f.InventoryId != inventory.Id))
            return new { message = "All fields must belong to the same inventory." };

        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user)))
            return new { message = "Forbidden." };

        foreach (var field in fieldsToDelete)
        {
            var itemsInInventory = _context.Items.Where(i => i.InventoryId == field.InventoryId);
            switch (field.Type)
            {
                case CustomFieldType.String:
                case CustomFieldType.Text:
                case CustomFieldType.FileUrl:
                    await itemsInInventory.ExecuteUpdateAsync(s => s.SetProperty(
                        i => EF.Property<string?>(i, field.TargetColumn),
                        (string?)null));
                    break;
                case CustomFieldType.Numeric:
                    await itemsInInventory.ExecuteUpdateAsync(s => s.SetProperty(
                        i => EF.Property<decimal?>(i, field.TargetColumn),
                        (decimal?)null));
                    break;
                case CustomFieldType.Bool:
                    await itemsInInventory.ExecuteUpdateAsync(s => s.SetProperty(
                        i => EF.Property<bool?>(i, field.TargetColumn),
                        (bool?)null));
                    break;
            }
        }

        _context.CustomFields.RemoveRange(fieldsToDelete);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return null; // Success
    }

    public async Task<object?> ReorderCustomFieldsAsync(string inventoryId, string[] orderedFieldIds, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return new { message = "Inventory not found." };
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return new { message = "Forbidden." };

        var fieldsToUpdate = await _context.CustomFields.Where(cf => cf.InventoryId == inventoryId).ToListAsync();
        if (fieldsToUpdate.Count != orderedFieldIds.Length || fieldsToUpdate.Any(f => !orderedFieldIds.Contains(f.Id)))
            return new { message = "The provided list of field IDs is incomplete or contains invalid IDs for this inventory." };

        for (int i = 0; i < orderedFieldIds.Length; i++)
        {
            var fieldId = orderedFieldIds[i];
            var field = fieldsToUpdate.FirstOrDefault(f => f.Id == fieldId);
            if (field != null) field.Order = i;
        }
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return null;
    }
}