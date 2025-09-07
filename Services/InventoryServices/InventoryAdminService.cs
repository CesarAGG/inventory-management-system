using InventoryManagementSystem.Data;
using InventoryManagementSystem.Helpers;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Models.CustomId;
using InventoryManagementSystem.ViewModels;
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

    public async Task<(IEnumerable<IdSegment>? Segments, object? Error)> GetIdFormatAsync(string inventoryId, ClaimsPrincipal user)
    {
        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return (null, new { message = "Inventory not found." });
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return (null, new { message = "Forbidden." });
        if (string.IsNullOrWhiteSpace(inventory.CustomIdFormat))
            return (new List<IdSegment>(), null);
        try
        {
            var segments = JsonIdSegmentDeserializer.Deserialize(inventory.CustomIdFormat);
            return (segments, null);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse stored CustomIdFormat for inventory {InventoryId}", inventoryId);
            return (null, new { message = "The stored ID format is corrupted." });
        }
    }

    public async Task<object?> SaveIdFormatAsync(string inventoryId, JsonElement format, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return new { message = "Inventory not found." };
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return new { message = "Forbidden." };

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
            return new { message = "The provided ID format was malformed." };
        }
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return null;
    }

    public async Task<(string? NewName, object? Error)> RenameInventoryAsync(string inventoryId, RenameInventoryRequest request, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return (null, new { message = "Inventory not found." });

        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return (null, new { message = "Forbidden." });

        inventory.Name = request.NewName;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return (inventory.Name, null);
    }

    public async Task<(string Message, bool ShouldRedirect, object? Error)> TransferOwnershipAsync(string inventoryId, TransferOwnershipRequest request, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return (string.Empty, false, new { message = "Inventory not found." });
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return (string.Empty, false, new { message = "Forbidden." });

        var newOwner = await _userManager.FindByEmailAsync(request.NewOwnerEmail);
        if (newOwner == null) return (string.Empty, false, new { message = "The specified user does not exist." });
        if (newOwner.Id == inventory.OwnerId) return (string.Empty, false, new { message = "This user is already the owner." });

        bool shouldRedirect = !IsAdmin(user);

        var newOwnerPermission = await _context.InventoryUserPermissions
            .FirstOrDefaultAsync(p => p.InventoryId == inventoryId && p.UserId == newOwner.Id);
        if (newOwnerPermission != null) _context.InventoryUserPermissions.Remove(newOwnerPermission);

        inventory.OwnerId = newOwner.Id;

        var oldOwnerId = inventory.OwnerId;
        var oldOwnerPermission = await _context.InventoryUserPermissions
            .FirstOrDefaultAsync(p => p.InventoryId == inventoryId && p.UserId == oldOwnerId);
        if (oldOwnerPermission != null) _context.InventoryUserPermissions.Remove(oldOwnerPermission);

        inventory.OwnerId = newOwner.Id;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ($"Ownership successfully transferred to {newOwner.Email}.", shouldRedirect, null);
    }

    public async Task<object?> DeleteInventoryAsync(string inventoryId, ClaimsPrincipal user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return new { message = "Inventory not found." };
        if (!_accessService.CanManageSettings(inventory, GetUserId(user), IsAdmin(user))) return new { message = "Forbidden." };

        _context.Inventories.Remove(inventory);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return null;
    }
}