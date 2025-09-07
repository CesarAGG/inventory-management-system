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

    public async Task<(object? Data, string? Error)> GetItemsDataAsync(string inventoryId)
    {
        var inventoryExists = await _context.Inventories.AnyAsync(i => i.Id == inventoryId);
        if (!inventoryExists)
        {
            return (null, "Inventory not found.");
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

        return (new { fields, items = itemDtos }, null);
    }

    public async Task<(ItemDto? Item, object? Error)> CreateItemAsync(string inventoryId, ItemApiRequest request, ClaimsPrincipal user)
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var inventory = await _context.Inventories.FirstOrDefaultAsync(inv => inv.Id == inventoryId);
            if (inventory == null) { await transaction.RollbackAsync(); return (null, new { message = "Inventory not found." }); }
            if (!await _accessService.CanWrite(inventory, GetUserId(user), IsAdmin(user))) { await transaction.RollbackAsync(); return (null, new { message = "Forbidden." }); }

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
            if (validationErrors.Any()) { await transaction.RollbackAsync(); return (null, new { message = "Validation failed.", errors = validationErrors }); }

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
                return (createdItemDto, null);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                _logger.LogWarning(ex, "Unique constraint violation on CustomId. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);
                if (i == maxRetries - 1)
                {
                    await transaction.RollbackAsync();
                    return (null, new { message = "Failed to generate a unique item ID after multiple attempts." });
                }
            }
        }
        return (null, new { message = "An unexpected error occurred while creating the item." });
    }

    public async Task<(object? UpdatedItem, object? Error)> UpdateItemAsync(string itemId, ItemApiRequest request, ClaimsPrincipal user)
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var itemToUpdate = await _context.Items.Include(it => it.Inventory).FirstOrDefaultAsync(it => it.Id == itemId);
            if (itemToUpdate == null || itemToUpdate.Inventory == null) { await transaction.RollbackAsync(); return (null, new { message = "Item not found." }); }
            if (!await _accessService.CanWrite(itemToUpdate.Inventory, GetUserId(user), IsAdmin(user))) { await transaction.RollbackAsync(); return (null, new { message = "Forbidden." }); }

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
            if (validationErrors.Any()) { await transaction.RollbackAsync(); return (null, validationErrors); }

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
                return (new { message = "OK" }, null);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                _logger.LogWarning(ex, "Unique constraint violation on CustomId during UPDATE. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);
                if (i == maxRetries - 1)
                {
                    await transaction.RollbackAsync();
                    return (null, new { message = "Failed to generate a unique item ID after multiple attempts." });
                }
            }
        }
        return (null, new { message = "An unexpected error occurred while updating the item." });
    }

    public async Task<object?> DeleteItemsAsync(string[] itemIds, ClaimsPrincipal user)
    {
        if (itemIds == null || !itemIds.Any()) return new { message = "No item IDs provided." };

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var itemsToDelete = await _context.Items.Where(i => itemIds.Contains(i.Id)).Include(i => i.Inventory).ToListAsync();
        if (itemsToDelete.Count != itemIds.Length) return new { message = "One or more items could not be found." };
        if (!itemsToDelete.Any()) return null;

        var inventory = itemsToDelete.First().Inventory;
        if (inventory == null || itemsToDelete.Any(i => i.InventoryId != inventory.Id))
            return new { message = "All items must belong to the same inventory for a batch delete." };

        if (!await _accessService.CanWrite(inventory, GetUserId(user), IsAdmin(user))) return new { message = "Forbidden." };

        _context.Items.RemoveRange(itemsToDelete);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return null;
    }
}