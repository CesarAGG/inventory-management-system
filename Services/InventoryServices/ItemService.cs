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
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.Models.CustomId;
using System.Text;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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

    private bool IsIdValid(
        string id,
        List<IdSegment> formatSegments,
        string? boundaries,
        Dictionary<string, string> validationErrors
    )
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
                SequenceSegment s => new Func<bool>(() =>
                {
                    // Rule 1: Must be a valid number. Use long for robustness.
                    if (!long.TryParse(idPart, out _))
                    {
                        return false;
                    }
                    // Rule 2: Length must be at least the padding value.
                    if (idPart.Length < s.Padding)
                    {
                        return false;
                    }
                    // Rule 3: If it has leading zeros (and isn't just "0"),
                    // the length must match the padding exactly.
                    if (idPart.Length > 1 && idPart.StartsWith("0"))
                    {
                        return idPart.Length == s.Padding;
                    }
                    // If no leading zeros, any length >= padding is valid.
                    return true;
                })(),
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

        if (string.IsNullOrEmpty(request.CustomId)) // ID Generation path
        {
            var result = _customIdService.GenerateId(inventory, segments);
            item.CustomId = result.Id;
            item.CustomIdSegmentBoundaries = result.Boundaries;
        }
        else // User-provided ID path
        {
            if (!IsIdValid(request.CustomId, segments, request.CustomIdSegmentBoundaries, validationErrors))
            {
                return;
            }
            item.CustomId = request.CustomId;
            item.CustomIdSegmentBoundaries = request.CustomIdSegmentBoundaries;

            // Robust logic to update sequence value from user-provided ID
            var boundariesStr = request.CustomIdSegmentBoundaries;
            if (!string.IsNullOrEmpty(boundariesStr))
            {
                var boundaries = boundariesStr.Split(',').Select(s => int.TryParse(s, out int val) ? val : -1).ToArray();
                if (boundaries.Length == segments.Count && boundaries.All(b => b != -1))
                {
                    int currentIndex = 0;
                    for (int i = 0; i < segments.Count; i++)
                    {
                        var segment = segments[i];
                        int length = boundaries[i];
                        if (request.CustomId.Length < currentIndex + length) break;
                        string segmentValue = request.CustomId.Substring(currentIndex, length);
                        currentIndex += length;

                        if (segment is SequenceSegment)
                        {
                            if (long.TryParse(segmentValue, out long userSequenceValue))
                            {
                                if (userSequenceValue > inventory.LastSequenceValue)
                                {
                                    inventory.LastSequenceValue = (int)userSequenceValue;
                                }
                            }
                            // Pragmatically enforce the current "one sequence segment" limitation.
                            break;
                        }
                    }
                }
            }
        }
        item.CustomIdFormatHashApplied = inventory.CustomIdFormatHash;
    }

    public async Task<ServiceResult<(string Id, string Boundaries, int NewSequenceValue)>> RegenerateIdAsync(string inventoryId, RegenerateIdRequest request, ClaimsPrincipal user)
    {
        var inventoryState = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventoryState == null) return ServiceResult<(string, string, int)>.FromError(ServiceErrorType.NotFound, "Inventory not found.");

        if (inventoryState.Version != request.InventoryVersion)
        {
            return ServiceResult<(string, string, int)>.FromError(ServiceErrorType.Concurrency, "Inventory settings have changed. Please reload.");
        }

        if (!await _accessService.CanWrite(inventoryState, GetUserId(user), IsAdmin(user))) return ServiceResult<(string, string, int)>.FromError(ServiceErrorType.Forbidden, "Forbidden.");

        if (string.IsNullOrWhiteSpace(inventoryState.CustomIdFormat))
        {
            return ServiceResult<(string, string, int)>.Success((string.Empty, string.Empty, 0));
        }

        var segments = JsonIdSegmentDeserializer.Deserialize(inventoryState.CustomIdFormat);
        var sequenceSegment = segments.OfType<SequenceSegment>().FirstOrDefault();
        int sequenceToUseForGeneration;

        // If the client provides a sequence value, it means we are re-randomizing an existing preview.
        // In this case, we use the provided value directly *without incrementing it* for the generation call.
        if (request.LastKnownSequenceValue.HasValue)
        {
            sequenceToUseForGeneration = request.LastKnownSequenceValue.Value;
        }
        else // This is the "zeroth click" call when the modal opens.
        {
            int currentDbSequence = inventoryState.LastSequenceValue;
            // Ensure the sequence starts correctly if it's below the defined start value.
            if (sequenceSegment != null && currentDbSequence < sequenceSegment.StartValue)
            {
                currentDbSequence = sequenceSegment.StartValue - sequenceSegment.Step;
            }
            // Calculate the *next* sequence value.
            sequenceToUseForGeneration = currentDbSequence + (sequenceSegment?.Step ?? 1);
        }

        // The generation service expects the *base* value to increment from, so we subtract one step.
        var tempInventoryForGeneration = new Inventory { LastSequenceValue = sequenceToUseForGeneration - (sequenceSegment?.Step ?? 1) };
        var result = _customIdService.GenerateId(tempInventoryForGeneration, segments);

        // Always return the sequence number that was *used* in this generation,
        // so the client can hold it for the next regeneration request.
        return ServiceResult<(string, string, int)>.Success((result.Id, result.Boundaries, sequenceToUseForGeneration));
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
        // Step 1: Get the full schema to understand all possible columns.
        var schemaResult = await GetInventorySchemaAsync(inventoryId);
        if (!schemaResult.IsSuccess)
        {
            return ServiceResult<DataTablesResponse<Dictionary<string, object?>>>.FromError(schemaResult.ErrorType, schemaResult.ErrorMessage!);
        }
        var fullSchema = schemaResult.Data!;

        var query = _context.Items.Where(i => i.InventoryId == inventoryId).AsNoTracking();
        var recordsTotal = await query.CountAsync();

        // Step 2: Correctly map the client's sort index (which is based on VISIBLE columns) back to a property name from the FULL schema.
        if (request.Order.Any())
        {
            var order = request.Order.First();
            var isDescending = order.Dir?.ToLower() == "desc";

            var visibleColumns = fullSchema.Columns.Where(c => c.IsVisibleInTable).ToList();
            var sortColumnDef = (order.Column >= 0 && order.Column < visibleColumns.Count) ? visibleColumns[order.Column] : null;

            if (sortColumnDef != null && !string.IsNullOrEmpty(sortColumnDef.Data) && sortColumnDef.Orderable)
            {
                Expression<Func<Item, object>> sortExpression = sortColumnDef.Data.ToLower() switch
                {
                    "customid" => i => i.CustomId,
                    "createdat" => i => i.CreatedAt,
                    "customstring1" => i => i.CustomString1 ?? string.Empty,
                    "customstring2" => i => i.CustomString2 ?? string.Empty,
                    "customstring3" => i => i.CustomString3 ?? string.Empty,
                    "customtext1" => i => i.CustomText1 ?? string.Empty,
                    "customtext2" => i => i.CustomText2 ?? string.Empty,
                    "customtext3" => i => i.CustomText3 ?? string.Empty,
                    "customnumeric1" => i => i.CustomNumeric1 ?? 0,
                    "customnumeric2" => i => i.CustomNumeric2 ?? 0,
                    "customnumeric3" => i => i.CustomNumeric3 ?? 0,
                    "custombool1" => i => i.CustomBool1 ?? false,
                    "custombool2" => i => i.CustomBool2 ?? false,
                    "custombool3" => i => i.CustomBool3 ?? false,
                    _ => i => i.CreatedAt
                };

                query = isDescending ? query.OrderByDescending(sortExpression) : query.OrderBy(sortExpression);
            }
            else
            {
                query = query.OrderByDescending(i => i.CreatedAt);
            }
        }
        else
        {
            query = query.OrderByDescending(i => i.CreatedAt);
        }

        // Step 3: ALWAYS return the full, stable data object for every item. The client will handle showing/hiding.
        var pagedData = await query
            .Skip(request.Start)
            .Take(request.Length)
            .Select(i => new Dictionary<string, object?>
            {
                { "id", i.Id }, { "customId", i.CustomId }, { "createdAt", i.CreatedAt },
                { "customidsegmentboundaries", i.CustomIdSegmentBoundaries }, { "customidformathashapplied", i.CustomIdFormatHashApplied },
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
        EntityEntry<Item>? itemEntry = null;

        for (int i = 0; i < maxRetries; i++)
        {
            var inventory = await _context.Inventories.FirstOrDefaultAsync(inv => inv.Id == inventoryId);
            if (inventory == null) { return ServiceResult<ItemDto>.FromError(ServiceErrorType.NotFound, "Inventory not found."); }
            if (!await _accessService.CanWrite(inventory, GetUserId(user), IsAdmin(user))) { return ServiceResult<ItemDto>.FromError(ServiceErrorType.Forbidden, "User does not have permission to write to this inventory."); }

            var fields = await _context.CustomFields.Where(cf => cf.InventoryId == inventoryId).AsNoTracking().ToListAsync();
            var newItem = new Item { Id = Guid.NewGuid().ToString(), InventoryId = inventoryId };
            var validationErrors = new Dictionary<string, string>();

            if (itemEntry != null)
            {
                itemEntry.State = EntityState.Detached;
            }

            HydrateItemFromRequest(newItem, request, fields, validationErrors);
            ApplyCustomId(newItem, inventory, request, validationErrors);
            if (validationErrors.Any()) { return ServiceResult<ItemDto>.FromValidationErrors(validationErrors); }

            itemEntry = _context.Items.Add(newItem);
            _context.Entry(inventory).State = EntityState.Modified; // Correctly mark inventory as modified

            try
            {
                await _context.SaveChangesAsync();
                var savedItem = itemEntry.Entity;
                var createdItemDto = new ItemDto
                {
                    Id = savedItem.Id,
                    CustomId = savedItem.CustomId,
                    CustomIdSegmentBoundaries = savedItem.CustomIdSegmentBoundaries,
                    CreatedAt = savedItem.CreatedAt,
                    Fields = fields.ToDictionary(field => field.TargetColumn.ToLower(), field => typeof(Item).GetProperty(field.TargetColumn)?.GetValue(savedItem)),
                    NewInventoryVersion = inventory.Version, // Return the NEW version
                    NewItemVersion = savedItem.Version
                };
                return ServiceResult<ItemDto>.Success(createdItemDto);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                if (pgEx.ConstraintName != null && pgEx.ConstraintName.Contains("IX_Items_InventoryId_CustomId"))
                {
                    _logger.LogWarning(ex, "CustomId collision on create. Retry {RetryCount}/{MaxRetries}", i + 1, maxRetries);
                    if (i == maxRetries - 1)
                    {
                        var errors = new Dictionary<string, string> { { "customId", "This Custom ID is already in use or a unique ID could not be generated. Please try saving again." } };
                        return ServiceResult<ItemDto>.FromValidationErrors(errors);
                    }
                }
                else
                {
                    return ServiceResult<ItemDto>.FromError(ServiceErrorType.General, "A data conflict occurred on another field.");
                }
            }
        }
        return ServiceResult<ItemDto>.FromError(ServiceErrorType.General, "An unexpected error occurred while creating the item.");
    }

    public async Task<ServiceResult<object>> UpdateItemAsync(string itemId, ItemApiRequest request, ClaimsPrincipal user)
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

        _context.Entry(itemToUpdate.Inventory).State = EntityState.Modified; // Correctly mark inventory as modified

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
            return ServiceResult<object>.FromError(ServiceErrorType.General, "A data conflict occurred on another field.");
        }
    }

    public async Task<ServiceResult<object>> ValidateCustomIdAsync(string inventoryId, ValidateIdRequest request, ClaimsPrincipal user)
    {
        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) { return ServiceResult<object>.FromError(ServiceErrorType.NotFound, "Inventory not found."); }
        if (!await _accessService.CanWrite(inventory, GetUserId(user), IsAdmin(user))) { return ServiceResult<object>.FromError(ServiceErrorType.Forbidden, "Forbidden."); }

        if (string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
        {
            return request.CustomId == ""
                ? ServiceResult<object>.Success(new { message = "OK" })
                : ServiceResult<object>.FromError(ServiceErrorType.InvalidInput, "A Custom ID is not allowed because no format is defined.");
        }

        var segments = JsonIdSegmentDeserializer.Deserialize(inventory.CustomIdFormat);
        var validationErrors = new Dictionary<string, string>();

        if (!IsIdValid(request.CustomId, segments, request.Boundaries, validationErrors))
        {
            return ServiceResult<object>.FromError(ServiceErrorType.InvalidInput, validationErrors.GetValueOrDefault("customId", "Invalid ID format."));
        }

        return ServiceResult<object>.Success(new { message = "OK" });
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