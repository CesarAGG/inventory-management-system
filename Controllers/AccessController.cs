using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory/{inventoryId}/access")]
public class AccessController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IInventoryAccessService _accessService;

    public AccessController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IInventoryAccessService accessService)
    {
        _context = context;
        _userManager = userManager;
        _accessService = accessService;
    }

    private string GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin() => User.IsInRole("Admin");

    [HttpGet]
    public async Task<IActionResult> GetAccessSettings(string inventoryId)
    {
        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        var permissions = await _context.InventoryUserPermissions
            .Where(p => p.InventoryId == inventoryId)
            .Select(p => new UserPermissionDto
            {
                UserId = p.UserId,
                UserEmail = p.User!.Email ?? string.Empty
            })
            .ToListAsync();

        var settings = new AccessSettingsDto
        {
            IsPublic = inventory.IsPublic,
            Permissions = permissions
        };

        return Ok(settings);
    }

    [HttpPut("public")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPublic(string inventoryId, [FromBody] bool isPublic)
    {
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        inventory.IsPublic = isPublic;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Public access setting updated." });
    }

    [HttpGet("search-users")]
    public async Task<IActionResult> SearchUsers(string inventoryId, [FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(new List<UserSearchDto>());
        }

        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        var usersWithPermission = await _context.InventoryUserPermissions
            .Where(p => p.InventoryId == inventoryId)
            .Select(p => p.UserId)
            .ToListAsync();

        var ownerId = inventory.OwnerId;

        IQueryable<ApplicationUser> userQuery = _userManager.Users;
        if (!IsAdmin())
        {
            // Non-admins must provide an exact email match (case-insensitive) to prevent enumeration.
            userQuery = userQuery.Where(u => u.Email != null && u.Email.ToLower() == query.ToLower());
        }
        else
        {
            // Admins can perform a partial "starts with" search.
            userQuery = userQuery.Where(u => u.Email != null && EF.Functions.ILike(u.Email, $"{query}%"));
        }

        var matchingUsers = await userQuery
            .Where(u => u.Id != ownerId && !usersWithPermission.Contains(u.Id))
            .Select(u => new UserSearchDto { Id = u.Id, Email = u.Email! })
            .Take(10)
            .ToListAsync();

        return Ok(matchingUsers);
    }

    [HttpPost("grant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantPermission(string inventoryId, [FromBody] string userId)
    {
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.Id == inventory.OwnerId)
        {
            return BadRequest("Invalid user specified.");
        }

        var existingPermission = await _context.InventoryUserPermissions.FindAsync(inventoryId, userId);
        if (existingPermission != null)
        {
            return Ok(new { message = "User already has permission." });
        }

        var newPermission = new InventoryUserPermission
        {
            InventoryId = inventoryId,
            UserId = userId,
            Level = PermissionLevel.Write
        };

        _context.InventoryUserPermissions.Add(newPermission);
        await _context.SaveChangesAsync();

        return Ok(new UserPermissionDto { UserId = user.Id, UserEmail = user.Email! });
    }

    [HttpPost("revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokePermissions(string inventoryId, [FromBody] string[] userIds)
    {
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, GetCurrentUserId(), IsAdmin())) return Forbid();

        if (userIds == null || !userIds.Any())
        {
            return BadRequest("No user IDs provided.");
        }

        var permissions = await _context.InventoryUserPermissions
            .Where(p => p.InventoryId == inventoryId && userIds.Contains(p.UserId))
            .ToListAsync();

        if (permissions.Any())
        {
            _context.InventoryUserPermissions.RemoveRange(permissions);
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Permissions revoked successfully." });
    }
}