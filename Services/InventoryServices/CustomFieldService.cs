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

    public async Task<ServiceResult<CustomFieldDto>> AddCustomFieldAsync(string inventoryId, CustomFieldDto newField, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return ServiceResult<CustomFieldDto>.FromError(ServiceErrorType.NotFound, "Inventory not found.");
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return ServiceResult<CustomFieldDto>.FromError(ServiceErrorType.Forbidden, "Forbidden.");

        if (string.IsNullOrWhiteSpace(newField.Name) || !Enum.TryParse<CustomFieldType>(newField.Type, out var fieldType))
            return ServiceResult<CustomFieldDto>.FromError(ServiceErrorType.InvalidInput, "Invalid field name or type.");

        var existingFields = await _context.CustomFields.Where(cf => cf.InventoryId == inventoryId && cf.Type == fieldType).ToListAsync();
        const int maxFieldsPerType = 3;
        if (existingFields.Count >= maxFieldsPerType)
            return ServiceResult<CustomFieldDto>.FromError(ServiceErrorType.InvalidInput, $"Cannot add another field of type '{fieldType}'. Maximum of {maxFieldsPerType} reached.");

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

        if (string.IsNullOrEmpty(targetColumn)) return ServiceResult<CustomFieldDto>.FromError(ServiceErrorType.General, "Could not find an available column for this field type.");

        var customField = new CustomField
        {
            InventoryId = inventoryId,
            Name = newField.Name,
            Description = newField.Description,
            IsVisibleInTable = newField.IsVisibleInTable,
            Type = fieldType,
            TargetColumn = targetColumn,
            Order = (existingFields.Max(f => (int?)f.Order) ?? -1) + 1
        };
        _context.CustomFields.Add(customField);

        // This pattern forces an update on the parent entity, which bumps the concurrency version token.
        // It's a pragmatic way to signal that a related collection has changed without needing a version on the join table itself.
        inventory.Name = inventory.Name;

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                return ServiceResult<CustomFieldDto>.FromError(ServiceErrorType.Concurrency, "A field with the same properties was created simultaneously. Please try again.");
            _logger.LogError(ex, "Database error while adding custom field for inventory {InventoryId}", inventoryId);
            return ServiceResult<CustomFieldDto>.FromError(ServiceErrorType.General, "A database error occurred. Please check your input (e.g., field name is not too long).");
        }

        var resultDto = new CustomFieldDto
        {
            Id = customField.Id,
            Name = customField.Name,
            Description = customField.Description,
            IsVisibleInTable = customField.IsVisibleInTable,
            Type = customField.Type.ToString(),
            DataKey = customField.TargetColumn,
            NewInventoryVersion = inventory.Version
        };
        return ServiceResult<CustomFieldDto>.Success(resultDto);
    }

    public async Task<ServiceResult<List<CustomFieldDto>>> GetCustomFieldsAsync(string inventoryId, ClaimsPrincipal user)
    {
        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return ServiceResult<List<CustomFieldDto>>.FromError(ServiceErrorType.NotFound, "Inventory not found.");
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return ServiceResult<List<CustomFieldDto>>.FromError(ServiceErrorType.Forbidden, "Forbidden.");

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId)
            .OrderBy(cf => cf.Order)
            .Select(cf => new CustomFieldDto
            {
                Id = cf.Id,
                Name = cf.Name,
                Description = cf.Description,
                IsVisibleInTable = cf.IsVisibleInTable,
                Type = cf.Type.ToString(),
                DataKey = cf.TargetColumn
            })
            .ToListAsync();
        return ServiceResult<List<CustomFieldDto>>.Success(fields);
    }

    public async Task<ServiceResult<object>> UpdateCustomFieldAsync(string fieldId, UpdateFieldRequest fieldUpdate, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var fieldToUpdate = await _context.CustomFields
            .Include(f => f.Inventory)
            .FirstOrDefaultAsync(f => f.Id == fieldId);

        if (fieldToUpdate == null || fieldToUpdate.Inventory == null)
            return ServiceResult<object>.FromError(ServiceErrorType.NotFound, "Field not found.");

        if (fieldToUpdate.Inventory.Version != fieldUpdate.InventoryVersion)
        {
            return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict: The inventory settings were modified by another user. Please reload and try again.");
        }

        if (!_accessService.CanManageSettings(fieldToUpdate.Inventory, GetUserId(user), IsAdmin(user)))
            return ServiceResult<object>.FromError(ServiceErrorType.Forbidden, "User does not have permission to manage this inventory.");

        if (string.IsNullOrWhiteSpace(fieldUpdate.Name))
            return ServiceResult<object>.FromError(ServiceErrorType.InvalidInput, "Field name cannot be empty.");

        fieldToUpdate.Name = fieldUpdate.Name;
        fieldToUpdate.Description = fieldUpdate.Description;
        fieldToUpdate.IsVisibleInTable = fieldUpdate.IsVisibleInTable;

        // This pattern forces an update on the parent entity, which bumps the concurrency version token.
        fieldToUpdate.Inventory.Name = fieldToUpdate.Inventory.Name;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var newVersion = fieldToUpdate.Inventory.Version;
        return ServiceResult<object>.Success(new { message = "OK", newVersion });
    }

    public async Task<ServiceResult<object>> DeleteCustomFieldsAsync(FieldDeleteRequest deleteRequest, ClaimsPrincipal user)
    {
        if (deleteRequest.FieldIds == null || !deleteRequest.FieldIds.Any())
            return ServiceResult<object>.FromError(ServiceErrorType.InvalidInput, "No field IDs provided.");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var fieldsToDelete = await _context.CustomFields
            .Include(cf => cf.Inventory)
            .Where(cf => deleteRequest.FieldIds.Contains(cf.Id))
            .ToListAsync();

        if (!fieldsToDelete.Any())
            return ServiceResult<object>.Success(new { message = "OK" });

        var inventory = fieldsToDelete.First().Inventory;
        if (inventory == null || fieldsToDelete.Any(f => f.InventoryId != inventory.Id))
            return ServiceResult<object>.FromError(ServiceErrorType.InvalidInput, "All fields must belong to the same inventory.");

        if (inventory.Version != deleteRequest.InventoryVersion)
        {
            return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict: The inventory settings were modified by another user. Please reload and try again.");
        }

        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user)))
            return ServiceResult<object>.FromError(ServiceErrorType.Forbidden, "User does not have permission to manage this inventory.");

        var itemsQuery = _context.Items.Where(i => i.InventoryId == inventory.Id);

        foreach (var field in fieldsToDelete)
        {
            switch (field.Type)
            {
                case CustomFieldType.String:
                case CustomFieldType.Text:
                case CustomFieldType.FileUrl:
                    await itemsQuery.ExecuteUpdateAsync(s => s.SetProperty(item => EF.Property<string?>(item, field.TargetColumn), (string?)null));
                    break;
                case CustomFieldType.Numeric:
                    await itemsQuery.ExecuteUpdateAsync(s => s.SetProperty(item => EF.Property<decimal?>(item, field.TargetColumn), (decimal?)null));
                    break;
                case CustomFieldType.Bool:
                    await itemsQuery.ExecuteUpdateAsync(s => s.SetProperty(item => EF.Property<bool?>(item, field.TargetColumn), (bool?)null));
                    break;
            }
        }

        _context.CustomFields.RemoveRange(fieldsToDelete);

        // This pattern forces an update on the parent entity, which bumps the concurrency version token.
        inventory.Name = inventory.Name;

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict occurred. Please reload and try again.");
        }

        var newVersion = inventory.Version;
        return ServiceResult<object>.Success(new { message = "OK", newVersion });
    }

    public async Task<ServiceResult<object>> ReorderCustomFieldsAsync(string inventoryId, FieldReorderRequest reorderRequest, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);

        if (inventory == null) return ServiceResult<object>.FromError(ServiceErrorType.NotFound, "Inventory not found.");

        if (inventory.Version != reorderRequest.InventoryVersion)
        {
            return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict: The inventory settings were modified by another user. Please reload and try again.");
        }

        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user)))
            return ServiceResult<object>.FromError(ServiceErrorType.Forbidden, "User does not have permission to manage this inventory.");

        var fieldsToUpdate = await _context.CustomFields.Where(cf => cf.InventoryId == inventoryId).ToListAsync();
        if (fieldsToUpdate.Count != reorderRequest.OrderedFieldIds.Length || fieldsToUpdate.Any(f => !reorderRequest.OrderedFieldIds.Contains(f.Id)))
            return ServiceResult<object>.FromError(ServiceErrorType.InvalidInput, "The provided list of field IDs is incomplete or contains invalid IDs for this inventory.");

        for (int i = 0; i < reorderRequest.OrderedFieldIds.Length; i++)
        {
            var fieldId = reorderRequest.OrderedFieldIds[i];
            var field = fieldsToUpdate.FirstOrDefault(f => f.Id == fieldId);
            if (field != null) field.Order = i;
        }

        // This pattern forces an update on the parent entity, which bumps the concurrency version token.
        inventory.Name = inventory.Name;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var newVersion = inventory.Version;
        return ServiceResult<object>.Success(new { message = "OK", newVersion });
    }
}