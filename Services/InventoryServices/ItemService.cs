using InventoryManagementSystem.Data;
using InventoryManagementSystem.Helpers;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.Services.InventoryServices;

public class ItemService : IItemService
{
    private readonly ApplicationDbContext _context;
    private readonly IInventoryAccessService _accessService;
    private readonly ICustomIdService _customIdService;
    private readonly ILogger<ItemService> _logger;

    public ItemService(ApplicationDbContext context, IInventoryAccessService accessService, ICustomIdService customIdService, ILogger<ItemService> logger)
    {
        _context = context;
        _accessService = accessService;
        _customIdService = customIdService;
        _logger = logger;
    }

    private string GetUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("Admin");

    public async Task<ServiceResult<object>> GetItemsDataAsync(string inventoryId)
    {
        var inventoryExists = await _context.Inventories.AnyAsync(i => i.Id == inventoryId);
        if (!inventoryExists)
        {
            return ServiceResult<object>.FromError(ServiceErrorType.NotFound, "Inventory not found.");
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

        var data = new { fields, items = itemDtos };
        return ServiceResult<object>.Success(data);
    }

    private void HydrateItemFromRequest(Item item, ItemApiRequest request, List<CustomField> fields, Dictionary<string, string> validationErrors)
    {
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
                        propInfo.SetValue(item, convertedValue);
                    }
                    catch (Exception ex) when (ex is FormatException || ex is InvalidCastException)
                    {
                        validationErrors[field.Id] = $"Invalid value for '{field.Name}'.";
                    }
                }
            }
        }
    }

    private void ApplyCustomId(Item item, Inventory inventory)
    {
        if (string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
        {
            item.CustomId = string.Empty;
            item.CustomIdFormatHashApplied = null;
            return;
        }

        bool needsNewId = string.IsNullOrEmpty(item.CustomId) || item.CustomIdFormatHashApplied != inventory.CustomIdFormatHash;
        if (needsNewId)
        {
            var segments = JsonIdSegmentDeserializer.Deserialize(inventory.CustomIdFormat);
            if (segments.Any())
            {
                item.CustomId = _customIdService.GenerateId(inventory, segments);
                item.CustomIdFormatHashApplied = inventory.CustomIdFormatHash;
            }
        }
    }

    public async Task<ServiceResult<ItemDto>> CreateItemAsync(string inventoryId, ItemApiRequest request, ClaimsPrincipal user)
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var inventory = await _context.Inventories.FirstOrDefaultAsync(inv => inv.Id == inventoryId);
            if (inventory == null) { return ServiceResult<ItemDto>.FromError(ServiceErrorType.NotFound, "Inventory not found."); }
            if (!await _accessService.CanWrite(inventory, GetUserId(user), IsAdmin(user))) { return ServiceResult<ItemDto>.FromError(ServiceErrorType.Forbidden, "User does not have permission to write to this inventory."); }

            var fields = await _context.CustomFields.Where(cf => cf.InventoryId == inventoryId).AsNoTracking().ToListAsync();
            var newItem = new Item { Id = Guid.NewGuid().ToString(), InventoryId = inventoryId };
            var validationErrors = new Dictionary<string, string>();

            HydrateItemFromRequest(newItem, request, fields, validationErrors);
            if (validationErrors.Any()) { return ServiceResult<ItemDto>.FromValidationErrors(validationErrors); }

            ApplyCustomId(newItem, inventory);
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
                return ServiceResult<ItemDto>.Success(createdItemDto);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                _logger.LogWarning(ex, "Unique constraint violation on CustomId. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);
                if (i == maxRetries - 1)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<ItemDto>.FromError(ServiceErrorType.General, "Failed to generate a unique item ID after multiple attempts.");
                }
            }
        }
        return ServiceResult<ItemDto>.FromError(ServiceErrorType.General, "An unexpected error occurred while creating the item.");
    }

    public async Task<ServiceResult<object>> UpdateItemAsync(string itemId, ItemApiRequest request, ClaimsPrincipal user)
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var itemToUpdate = await _context.Items.Include(it => it.Inventory).FirstOrDefaultAsync(it => it.Id == itemId);
            if (itemToUpdate == null || itemToUpdate.Inventory == null) { return ServiceResult<object>.FromError(ServiceErrorType.NotFound, "Item not found."); }
            if (!await _accessService.CanWrite(itemToUpdate.Inventory, GetUserId(user), IsAdmin(user))) { return ServiceResult<object>.FromError(ServiceErrorType.Forbidden, "User does not have permission to write to this inventory."); }

            var fields = await _context.CustomFields.Where(cf => cf.InventoryId == itemToUpdate.InventoryId).AsNoTracking().ToListAsync();
            var validationErrors = new Dictionary<string, string>();

            HydrateItemFromRequest(itemToUpdate, request, fields, validationErrors);
            if (validationErrors.Any()) { return ServiceResult<object>.FromValidationErrors(validationErrors); }

            ApplyCustomId(itemToUpdate, itemToUpdate.Inventory);

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return ServiceResult<object>.Success(new { message = "OK" });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict: This item was modified by another user. Please reload and try again.");
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                _logger.LogWarning(ex, "Unique constraint violation on CustomId during UPDATE. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);
                if (i == maxRetries - 1)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<object>.FromError(ServiceErrorType.General, "Failed to generate a unique item ID after multiple attempts.");
                }
            }
        }
        return ServiceResult<object>.FromError(ServiceErrorType.General, "An unexpected error occurred while updating the item.");
    }

    public async Task<ServiceResult<object>> DeleteItemsAsync(string[] itemIds, ClaimsPrincipal user)
    {
        if (itemIds == null || !itemIds.Any()) return ServiceResult<object>.FromError(ServiceErrorType.InvalidInput, "No item IDs provided.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var itemsToDelete = await _context.Items.Where(i => itemIds.Contains(i.Id)).Include(i => i.Inventory).ToListAsync();
        if (itemsToDelete.Count != itemIds.Length) return ServiceResult<object>.FromError(ServiceErrorType.NotFound, "One or more items could not be found.");
        if (!itemsToDelete.Any()) return ServiceResult<object>.Success(new { message = "OK" });

        var inventory = itemsToDelete.First().Inventory;
        if (inventory == null || itemsToDelete.Any(i => i.InventoryId != inventory.Id))
            return ServiceResult<object>.FromError(ServiceErrorType.InvalidInput, "All items must belong to the same inventory for a batch delete.");

        if (!await _accessService.CanWrite(inventory, GetUserId(user), IsAdmin(user))) return ServiceResult<object>.FromError(ServiceErrorType.Forbidden, "User does not have permission to delete items from this inventory.");

        _context.Items.RemoveRange(itemsToDelete);
        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict: One or more of the selected items were modified by another user. Please reload and try again.");
        }

        return ServiceResult<object>.Success(new { message = "Deleted" });
    }
}