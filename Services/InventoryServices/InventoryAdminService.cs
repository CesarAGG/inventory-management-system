using InventoryManagementSystem.Data;
using InventoryManagementSystem.Helpers;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Models.CustomId;
using InventoryManagementSystem.ViewModels;
using InventoryManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services.InventoryServices;

public class InventoryAdminService : IInventoryAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly IInventoryAccessService _accessService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InventoryAdminService> _logger;

    public InventoryAdminService(ApplicationDbContext context, IInventoryAccessService accessService, UserManager<ApplicationUser> userManager, ILogger<InventoryAdminService> logger)
    {
        _context = context;
        _accessService = accessService;
        _userManager = userManager;
        _logger = logger;
    }

    private string GetUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("Admin");

    public async Task<ServiceResult<IEnumerable<IdSegment>>> GetIdFormatAsync(string inventoryId, ClaimsPrincipal user)
    {
        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return ServiceResult<IEnumerable<IdSegment>>.FromError(ServiceErrorType.NotFound, "Inventory not found.");
        
        if (!await _accessService.CanWrite(inventory, GetUserId(user), IsAdmin(user)))
        {
            return ServiceResult<IEnumerable<IdSegment>>.FromError(ServiceErrorType.Forbidden, "User does not have permission to view the ID format for this inventory.");
        }

        if (string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
            return ServiceResult<IEnumerable<IdSegment>>.Success(new List<IdSegment>());
        try
        {
            var segments = JsonIdSegmentDeserializer.Deserialize(inventory.CustomIdFormat);
            return ServiceResult<IEnumerable<IdSegment>>.Success(segments);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse stored CustomIdFormat for inventory {InventoryId}", inventoryId);
            return ServiceResult<IEnumerable<IdSegment>>.FromError(ServiceErrorType.General, "The stored ID format is corrupted.");
        }
    }

    public async Task<ServiceResult<object>> SaveIdFormatAsync(string inventoryId, JsonElement format, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return ServiceResult<object>.FromError(ServiceErrorType.NotFound, "Inventory not found.");
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return ServiceResult<object>.FromError(ServiceErrorType.Forbidden, "User does not have permission to manage this inventory.");

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
            return ServiceResult<object>.FromError(ServiceErrorType.InvalidInput, "The provided ID format was malformed.");
        }
        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict: These settings were modified by another user. Please reload and try again.");
        }
        return ServiceResult<object>.Success(new { message = "OK", newVersion = inventory.Version, newHash = inventory.CustomIdFormatHash });
    }

    public async Task<ServiceResult<object>> RenameInventoryAsync(string inventoryId, RenameInventoryRequest request, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return ServiceResult<object>.FromError(ServiceErrorType.NotFound, "Inventory not found.");

        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return ServiceResult<object>.FromError(ServiceErrorType.Forbidden, "User does not have permission to manage this inventory.");

        inventory.Name = request.NewName;
        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict: This inventory was modified by another user. Please reload and try again.");
        }
        return ServiceResult<object>.Success(new { newName = inventory.Name, newVersion = inventory.Version });
    }

    public async Task<ServiceResult<TransferOwnershipResponse>> TransferOwnershipAsync(string inventoryId, TransferOwnershipRequest request, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return ServiceResult<TransferOwnershipResponse>.FromError(ServiceErrorType.NotFound, "Inventory not found.");
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return ServiceResult<TransferOwnershipResponse>.FromError(ServiceErrorType.Forbidden, "User does not have permission to manage this inventory.");

        var newOwner = await _userManager.FindByEmailAsync(request.NewOwnerEmail);
        if (newOwner == null) return ServiceResult<TransferOwnershipResponse>.FromError(ServiceErrorType.InvalidInput, "The specified user does not exist.");
        if (newOwner.IsBlocked) return ServiceResult<TransferOwnershipResponse>.FromError(ServiceErrorType.InvalidInput, "Cannot transfer ownership to a blocked user.");
        if (newOwner.Id == inventory.OwnerId) return ServiceResult<TransferOwnershipResponse>.FromError(ServiceErrorType.InvalidInput, "This user is already the owner.");

        bool shouldRedirect = !IsAdmin(user);

        var newOwnerPermission = await _context.InventoryUserPermissions
            .FirstOrDefaultAsync(p => p.InventoryId == inventoryId && p.UserId == newOwner.Id);
        if (newOwnerPermission != null) _context.InventoryUserPermissions.Remove(newOwnerPermission);

        var oldOwnerId = inventory.OwnerId;
        inventory.OwnerId = newOwner.Id;

        var oldOwnerPermission = await _context.InventoryUserPermissions
            .FirstOrDefaultAsync(p => p.InventoryId == inventoryId && p.UserId == oldOwnerId);
        if (oldOwnerPermission != null) _context.InventoryUserPermissions.Remove(oldOwnerPermission);

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<TransferOwnershipResponse>.FromError(ServiceErrorType.Concurrency, "Data conflict: This inventory was modified by another user. Please reload and try again.");
        }

        var resultData = new TransferOwnershipResponse
        {
            Message = $"Ownership successfully transferred to {newOwner.Email}.",
            ShouldRedirect = shouldRedirect
        };
        resultData.NewInventoryVersion = inventory.Version;
        return ServiceResult<TransferOwnershipResponse>.Success(resultData);
    }

    public async Task<ServiceResult<object>> DeleteInventoryAsync(string inventoryId, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return ServiceResult<object>.FromError(ServiceErrorType.NotFound, "Inventory not found.");
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return ServiceResult<object>.FromError(ServiceErrorType.Forbidden, "User does not have permission to manage this inventory.");

        _context.Inventories.Remove(inventory);
        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<object>.FromError(ServiceErrorType.Concurrency, "Data conflict: This inventory was modified by another user. Please reload and try again.");
        }

        return ServiceResult<object>.Success(new { message = "Inventory and all its data have been permanently deleted." });
    }
}