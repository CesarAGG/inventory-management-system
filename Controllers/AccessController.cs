using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
    private readonly ILogger<AccessController> _logger;

    public AccessController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IInventoryAccessService accessService, ILogger<AccessController> logger)
    {
        _context = context;
        _userManager = userManager;
        _accessService = accessService;
        _logger = logger;
    }

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool IsAdmin() => User.IsInRole("Admin");

    [HttpGet]
    public async Task<IActionResult> GetAccessSettings(string inventoryId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null) return Unauthorized();

        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, currentUserId, IsAdmin())) return Forbid();

        var permissions = await _context.InventoryUserPermissions
            .Where(p => p.InventoryId == inventoryId)
            .Select(p => new UserPermissionDto
            {
                UserId = p.UserId,
                UserName = p.User!.UserName ?? string.Empty,
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
    public async Task<IActionResult> SetPublic(string inventoryId, [FromBody] UpdatePublicAccessRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null) return Unauthorized();

        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, currentUserId, IsAdmin())) return Forbid();

        if (inventory.Version != request.InventoryVersion)
        {
            return Conflict(new { message = "Data conflict: The inventory settings were modified by another user. Please reload and try again." });
        }

        inventory.IsPublic = request.IsPublic;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Public access setting updated.", newVersion = inventory.Version });
    }

    [HttpGet("search-users")]
    public async Task<IActionResult> SearchUsers(string inventoryId, [FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(new List<UserSearchDto>());
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null) return Unauthorized();

        var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, currentUserId, IsAdmin())) return Forbid();

        var usersWithPermission = await _context.InventoryUserPermissions
            .Where(p => p.InventoryId == inventoryId)
            .Select(p => p.UserId)
            .ToListAsync();

        var ownerId = inventory.OwnerId;

        var userQuery = _userManager.Users.AsQueryable();
        var lowerQuery = query.ToLower();

        if (!IsAdmin())
        {
            userQuery = userQuery.Where(u =>
                (u.Email != null && u.Email.ToLower() == lowerQuery) ||
                (u.UserName != null && u.UserName.ToLower() == lowerQuery)
            );
        }
        else
        {
            userQuery = userQuery.Where(u =>
                (u.Email != null && EF.Functions.ILike(u.Email, $"{query}%")) ||
                (u.UserName != null && EF.Functions.ILike(u.UserName, $"{query}%"))
            );
        }

        var matchingUsers = await userQuery
            .Where(u => u.Id != ownerId && !usersWithPermission.Contains(u.Id))
            .Select(u => new UserSearchDto { Id = u.Id, Email = u.Email ?? string.Empty, UserName = u.UserName ?? string.Empty })
            .Take(10)
            .ToListAsync();

        return Ok(matchingUsers);
    }

    [HttpPost("granted-users")]
    public async Task<IActionResult> GetGrantedUsers(string inventoryId, [FromBody] DataTablesRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null) return Unauthorized();

            var inventory = await _context.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
            if (inventory == null) return NotFound();
            if (!_accessService.CanManageSettings(inventory, currentUserId, IsAdmin())) return Forbid();

            var query = _context.InventoryUserPermissions
                .Where(p => p.InventoryId == inventoryId)
                .Select(p => p.User)
                .Where(u => u != null)
                .Select(u => u!);

            var recordsTotal = await query.CountAsync();

            if (request.Order.Any())
            {
                var order = request.Order.First();
                var isDescending = order.Dir?.ToLower() == "desc";

                // Hardened sorting expressions to prevent NullReferenceException
                Expression<Func<ApplicationUser, object>> sortColumnExpression = order.Column switch
                {
                    2 => u => u.Email ?? string.Empty,
                    _ => u => u.UserName ?? string.Empty
                };

                query = isDescending
                    ? query.OrderByDescending(sortColumnExpression)
                    : query.OrderBy(sortColumnExpression);
            }
            else
            {
                // Harden the default sort as well
                query = query.OrderBy(u => u.UserName ?? string.Empty);
            }

            var pagedData = await query
                .Skip(request.Start)
                .Take(request.Length)
                .Select(u => new UserPermissionDto
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    UserEmail = u.Email ?? string.Empty
                })
                .ToListAsync();

            return Ok(new DataTablesResponse<UserPermissionDto>
            {
                Draw = request.Draw,
                RecordsTotal = recordsTotal,
                RecordsFiltered = recordsTotal,
                Data = pagedData
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching granted users for inventory {InventoryId}", inventoryId);
            return StatusCode(500, new { message = "An internal server error occurred. The error has been logged." });
        }
    }

    [HttpPost("grant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantPermission(string inventoryId, [FromBody] GrantPermissionRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null) return Unauthorized();

        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, currentUserId, IsAdmin())) return Forbid();

        if (inventory.Version != request.InventoryVersion)
        {
            return Conflict(new { message = "Data conflict: The inventory settings were modified by another user. Please reload and try again." });
        }

        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null || user.Id == inventory.OwnerId)
        {
            return BadRequest("Invalid user specified.");
        }

        var existingPermission = await _context.InventoryUserPermissions.FindAsync(inventoryId, request.UserId);
        if (existingPermission != null)
        {
            return Ok(new { message = "User already has permission." });
        }

        var newPermission = new InventoryUserPermission
        {
            InventoryId = inventoryId,
            UserId = request.UserId,
            Level = PermissionLevel.Write
        };

        _context.InventoryUserPermissions.Add(newPermission);
        inventory.Name = inventory.Name;
        await _context.SaveChangesAsync();
        return Ok(new UserPermissionDto { UserId = user.Id, UserName = user.UserName ?? string.Empty, UserEmail = user.Email ?? string.Empty, NewInventoryVersion = inventory.Version });
    }

    [HttpPost("revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokePermissions(string inventoryId, [FromBody] RevokePermissionsRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null) return Unauthorized();

        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory == null) return NotFound();
        if (!_accessService.CanManageSettings(inventory, currentUserId, IsAdmin())) return Forbid();

        if (inventory.Version != request.InventoryVersion)
        {
            return Conflict(new { message = "Data conflict: The inventory settings were modified by another user. Please reload and try again." });
        }

        if (request.UserIds == null || !request.UserIds.Any())
        {
            return BadRequest("No user IDs provided.");
        }

        var permissions = await _context.InventoryUserPermissions
            .Where(p => p.InventoryId == inventoryId && request.UserIds.Contains(p.UserId))
            .ToListAsync();

        if (permissions.Any())
        {
            _context.InventoryUserPermissions.RemoveRange(permissions);
            // This pattern forces an update on the parent entity, which bumps the concurrency version token.
            inventory.Name = inventory.Name;
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Permissions revoked successfully.", newVersion = inventory.Version });
    }
}