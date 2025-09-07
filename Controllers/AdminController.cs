using InventoryManagementSystem.Data;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using Npgsql;

namespace InventoryManagementSystem.Controllers;

public class UserViewModel
{
    public ApplicationUser User { get; set; } = new();
    public IList<string> Roles { get; set; } = new List<string>();
}

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    // GET: /Admin/Index
    public async Task<IActionResult> Index()
    {
        var userViewModels = await _context.Users
            .AsNoTracking() // Read-only query for better performance
            .Select(user => new UserViewModel
            {
                User = user,
                Roles = (from userRole in _context.UserRoles
                         join role in _context.Roles on userRole.RoleId equals role.Id
                         where userRole.UserId == user.Id
                         select role.Name).ToList()
            })
            .ToListAsync();

        return View(userViewModels);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockUsers(string[] selectedUserIds)
    {
        await UpdateUserBlockStatus(selectedUserIds, true);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockUsers(string[] selectedUserIds)
    {
        await UpdateUserBlockStatus(selectedUserIds, false);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUsers(string[] selectedUserIds)
    {
        var usersToDelete = await _userManager.Users
            .Where(u => selectedUserIds.Contains(u.Id))
            .ToListAsync();

        var deletedCount = 0;
        foreach (var user in usersToDelete)
        {
            try
            {
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    deletedCount++;
                }
                else
                {
                    // Handle other potential Identity errors, e.g., concurrency failure
                    TempData["ErrorMessage"] = $"Could not delete user {user.Email}. An unexpected error occurred.";
                }
            }
            catch (DbUpdateException ex)
            {
                // Check if the inner exception is the specific PostgreSQL foreign key violation.
                if (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23503")
                {
                    TempData["ErrorMessage"] = $"Cannot delete user {user.Email} because they own one or more inventories. Please transfer ownership of their inventories first.";
                }
                else
                {
                    // Handle other, unexpected database errors.
                    TempData["ErrorMessage"] = $"A database error occurred while trying to delete {user.Email}.";
                }
            }
        }

        if (deletedCount > 0)
        {
            TempData["SuccessMessage"] = $"{deletedCount} user(s) deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignAdminRole(string[] selectedUserIds)
    {
        await UpdateUserAdminRole(selectedUserIds, true);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdminRole(string[] selectedUserIds)
    {
        await UpdateUserAdminRole(selectedUserIds, false);
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<ApplicationUser>> GetUsersByIdsAsync(string[] userIds)
    {
        return await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();
    }

    private async Task UpdateUserBlockStatus(string[] userIds, bool isBlocked)
    {
        var usersToUpdate = await GetUsersByIdsAsync(userIds);

        foreach (var user in usersToUpdate)
        {
            user.IsBlocked = isBlocked;
            await _userManager.UpdateAsync(user);
            // After updating the user, this invalidates their security stamp, forcing a logout on their next request
            await _userManager.UpdateSecurityStampAsync(user);
        }
    }

    private async Task UpdateUserAdminRole(string[] userIds, bool assignAdmin)
    {
        const string adminRoleName = "Admin";
        var usersToUpdate = await GetUsersByIdsAsync(userIds);

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        foreach (var user in usersToUpdate)
        {
            if (assignAdmin && !await _userManager.IsInRoleAsync(user, adminRoleName))
            {
                await _userManager.AddToRoleAsync(user, adminRoleName);
            }
            else if (!assignAdmin && await _userManager.IsInRoleAsync(user, adminRoleName))
            {
                await _userManager.RemoveFromRoleAsync(user, adminRoleName);
            }

            if (user.Id == currentUserId)
            {
                await _signInManager.RefreshSignInAsync(user);
            }
        }
    }
}
