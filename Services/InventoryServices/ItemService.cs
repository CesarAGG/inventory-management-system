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
using InventoryManagementSystem.Models.CustomId;

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

    private bool IsIdValid(string id, List<IdSegment> formatSegments, string? boundaries, Dictionary<string, string> validationErrors)
    {
        if (string.IsNullOrEmpty(boundaries))
        {
            validationErrors["customId"] = "ID structure (segment boundaries) is missing.";
            return false;
        }

        var boundaryParts = boundaries.Split(',').Select(p => int.TryParse(p, out var val) ? val : -1).ToArray();
        if (boundaryParts.Any(p => p == -1) || boundaryParts.Length != formatSegments.Count || boundaryParts.Sum() != id.Length)
        {
            validationErrors["customId"] = "ID structure is invalid or does not match the ID string's length.";
            return false;
        }

        int currentIndex = 0;
        for (int i = 0; i < formatSegments.Count; i++)
        {
            var segment = formatSegments[i];
            var length = boundaryParts[i];
            var idPart = id.Substring(currentIndex, length);

            bool segmentValid = segment switch
            {
                FixedTextSegment s => s.Value == idPart,
                DateSegment s => DateTime.TryParseExact(idPart, s.Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                SequenceSegment s => int.TryParse(idPart, out _) && idPart.Length >= s.Padding,
                RandomNumbersSegment s => s.Format switch
                {
                    "20-bit" => long.TryParse(idPart, out var num) && num >= 0 && num < 1048576,
                    "32-bit" => long.TryParse(idPart, out var num) && num >= 0 && num < 2147483648L,
                    "6-digit" => idPart.Length == 6 && int.TryParse(idPart, out _),
                    "9-digit" => idPart.Length == 9 && int.TryParse(idPart, out _),
                    _ => false
                },
                GuidSegment s => Guid.TryParseExact(idPart, s.Format, out _),
                _ => false
            };

            if (!segmentValid)
            {
                validationErrors["customId"] = $"The segment '{idPart}' is not valid for the type '{segment.Type}' with its format constraints.";
                return false;
            }
            currentIndex += length;
        }

        return true;
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

    private void ApplyCustomId(Item item, Inventory inventory, ItemApiRequest request, Dictionary<string, string> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
        {
            if (!string.IsNullOrEmpty(request.CustomId))
            {
                validationErrors["customId"] = "A Custom ID is not allowed because no format is defined for this inventory.";
            }
            item.CustomId = string.Empty;
            item.CustomIdFormatHashApplied = null;
            item.CustomIdSegmentBoundaries = null;
            return;
        }

        var segments = JsonIdSegmentDeserializer.Deserialize(inventory.CustomIdFormat);

        if (string.IsNullOrEmpty(request.CustomId))
        {
            var result = _customIdService.GenerateId(inventory, segments);
            item.CustomId = result.Id;
            item.CustomIdSegmentBoundaries = result.Boundaries;
        }
        else
        {
            if (!IsIdValid(request.CustomId, segments, request.CustomIdSegmentBoundaries, validationErrors))
            {
                return;
            }
            item.CustomId = request.CustomId;
            item.CustomIdSegmentBoundaries = request.CustomIdSegmentBoundaries;
        }
        item.CustomIdFormatHashApplied = inventory.CustomIdFormatHash;
    }

    public async Task<ServiceResult<(string Id, string Boundaries)>> RegenerateIdAsync(string inventoryId, uint inventoryVersion, ClaimsPrincipal user)
    {
        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return ServiceResult<(string, string)>.FromError(ServiceErrorType.NotFound, "Inventory not found.");

        if (inventory.Version != inventoryVersion)
        {
            return ServiceResult<(string, string)>.FromError(ServiceErrorType.Concurrency, "Inventory settings have changed. Please reload.");
        }

        if (!await _accessService.CanWrite(inventory, GetUserId(user), IsAdmin(user))) return ServiceResult<(string, string)>.FromError(ServiceErrorType.Forbidden, "Forbidden.");

        if (string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
        {
            return ServiceResult<(string, string)>.Success((string.Empty, string.Empty));
        }

        var segments = JsonIdSegmentDeserializer.Deserialize(inventory.CustomIdFormat);

        var tempInventoryState = new Inventory { LastSequenceValue = inventory.LastSequenceValue };
        var result = _customIdService.GenerateId(tempInventoryState, segments);

        return ServiceResult<(string, string)>.Success(result);
    }

    public async Task<ServiceResult<InventorySchemaViewModel>> GetInventorySchemaAsync(string inventoryId)
    {
        var inventoryExists = await _context.Inventories.AnyAsync(i => i.Id == inventoryId);
        if (!inventoryExists)
        {
            return ServiceResult<InventorySchemaViewModel>.FromError(ServiceErrorType.NotFound, "Inventory not found.");
        }

        var schema = new InventorySchemaViewModel();
        schema.Columns.Add(new ColumnDefinition { Title = "", Data = "id", Orderable = false, FieldId = "system_checkbox" });
        schema.Columns.Add(new ColumnDefinition { Title = "Item ID", Data = "customId", FieldId = "system_customId" });
        schema.Columns.Add(new ColumnDefinition { Title = "Created At", Data = "createdAt", FieldId = "system_createdAt" });

        var customFields = await _context.CustomFields
            .Where(cf => cf.InventoryId == inventoryId)
            .OrderBy(cf => cf.Order)
            .AsNoTracking()
            .Select(f => new { f.Id, f.Name, f.Description, f.TargetColumn, Type = f.Type.ToString(), f.IsVisibleInTable })
            .ToListAsync();

        foreach (var field in customFields)
        {
            schema.Columns.Add(new ColumnDefinition
            {
                FieldId = field.Id,
                Title = field.Name,
                Description = field.Description,
                Data = field.TargetColumn.ToLower(),
                Type = field.Type,
                IsVisibleInTable = field.IsVisibleInTable
            });
        }

        return ServiceResult<InventorySchemaViewModel>.Success(schema);
    }

    public async Task<ServiceResult<DataTablesResponse<Dictionary<string, object?>>>> GetItemsForDataTableAsync(string inventoryId, DataTablesRequest request)
    {
        var schemaResult = await GetInventorySchemaAsync(inventoryId);
        if (!schemaResult.IsSuccess)
        {
            return ServiceResult<DataTablesResponse<Dictionary<string, object?>>>.FromError(schemaResult.ErrorType, schemaResult.ErrorMessage!);
        }
        var schema = schemaResult.Data!;

        var query = _context.Items
            .Where(i => i.InventoryId == inventoryId)
            .AsNoTracking();

        var recordsTotal = await query.CountAsync();

        // Sorting
        if (request.Order.Any())
        {
            var order = request.Order.First();
            var sortDir = order.Dir?.ToLower() == "desc" ? "desc" : "asc";

            if (order.Column >= 0 && order.Column < schema.Columns.Count)
            {
                var sortColumn = schema.Columns[order.Column].Data;
                if (!string.IsNullOrEmpty(sortColumn) && schema.Columns[order.Column].Orderable)
                {
                    // Capitalize first letter to match EF Core property name
                    var efSortColumn = char.ToUpper(sortColumn[0]) + sortColumn.Substring(1);
                    query = query.OrderBy($"{efSortColumn} {sortDir}");
                }
            }
        }
        else
        {
            query = query.OrderByDescending(i => i.CreatedAt);
        }

        var pagedData = await query
            .Skip(request.Start)
            .Take(request.Length)
            .Select(i => new Dictionary<string, object?>
            {
                { "id", i.Id },
                { "customId", i.CustomId },
                { "createdAt", i.CreatedAt },
                { "customstring1", i.CustomString1 }, { "customstring2", i.CustomString2 }, { "customstring3", i.CustomString3 },
                { "customtext1", i.CustomText1 }, { "customtext2", i.CustomText2 }, { "customtext3", i.CustomText3 },
                { "customnumeric1", i.CustomNumeric1 }, { "customnumeric2", i.CustomNumeric2 }, { "customnumeric3", i.CustomNumeric3 },
                { "custombool1", i.CustomBool1 }, { "custombool2", i.CustomBool2 }, { "custombool3", i.CustomBool3 },
                { "customfileurl1", i.CustomFileUrl1 }, { "customfileurl2", i.CustomFileUrl2 }, { "customfileurl3", i.CustomFileUrl3 }
            })
            .ToListAsync();

        var response = new DataTablesResponse<Dictionary<string, object?>>
        {
            Draw = request.Draw,
            RecordsTotal = recordsTotal,
            RecordsFiltered = recordsTotal,
            Data = pagedData
        };

        return ServiceResult<DataTablesResponse<Dictionary<string, object?>>>.Success(response);
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
            ApplyCustomId(newItem, inventory, request, validationErrors);
            if (validationErrors.Any()) { return ServiceResult<ItemDto>.FromValidationErrors(validationErrors); }

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
                    CustomIdSegmentBoundaries = savedItem.CustomIdSegmentBoundaries,
                    CreatedAt = savedItem.CreatedAt,
                    Fields = fields.ToDictionary(field => field.TargetColumn.ToLower(), field => typeof(Item).GetProperty(field.TargetColumn)?.GetValue(savedItem)),
                    NewInventoryVersion = inventory.Version,
                    NewItemVersion = savedItem.Version
                };
                return ServiceResult<ItemDto>.Success(createdItemDto);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                await transaction.RollbackAsync();
                if (pgEx.ConstraintName != null && pgEx.ConstraintName.Contains("IX_Items_InventoryId_CustomId"))
                {
                    var errors = new Dictionary<string, string> { { "customId", "This Custom ID is already in use in this inventory." } };
                    return ServiceResult<ItemDto>.FromValidationErrors(errors);
                }

                _logger.LogWarning(ex, "Unique constraint violation on creating item. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);
                if (i == maxRetries - 1)
                {
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
            ApplyCustomId(itemToUpdate, itemToUpdate.Inventory, request, validationErrors);
            if (validationErrors.Any()) { return ServiceResult<object>.FromValidationErrors(validationErrors); }

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return ServiceResult<object>.Success(new { message = "OK", newInventoryVersion = itemToUpdate.Inventory.Version, newItemVersion = itemToUpdate.Version });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict: This item was modified by another user. Please reload and try again.");
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                await transaction.RollbackAsync();
                if (pgEx.ConstraintName != null && pgEx.ConstraintName.Contains("IX_Items_InventoryId_CustomId"))
                {
                    var errors = new Dictionary<string, string> { { "customId", "This Custom ID is already in use in this inventory." } };
                    return ServiceResult<object>.FromValidationErrors(errors);
                }

                _logger.LogWarning(ex, "Unique constraint violation on updating item. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);
                if (i == maxRetries - 1)
                {
                    return ServiceResult<object>.FromError(ServiceErrorType.General, "Failed to update item due to a data conflict.");
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

        return ServiceResult<object>.Success(new { message = "Deleted", newInventoryVersion = inventory.Version });
    }
}