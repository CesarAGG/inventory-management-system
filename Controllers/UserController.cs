using InventoryManagementSystem.Data;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers;

[Authorize]
public class UserController : Controller
{
    private readonly ApplicationDbContext _context;

    public UserController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Challenge();
        }

        var ownedInventories = await _context.Inventories
            .Where(i => i.OwnerId == userId)
            .AsNoTracking()
            .Select(i => new InventoryInfoViewModel
            {
                Id = i.Id,
                Name = i.Name,
                CreatedAt = i.CreatedAt,
                IsPublic = i.IsPublic,
                ItemCount = i.Items.Count()
            })
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var sharedInventories = await _context.Inventories
            .Where(i => i.Permissions.Any(p => p.UserId == userId && p.Level == Models.PermissionLevel.Write))
            .AsNoTracking()
            .Select(i => new InventoryInfoViewModel
            {
                Id = i.Id,
                Name = i.Name,
                CreatedAt = i.CreatedAt,
                ItemCount = i.Items.Count(),
                OwnerEmail = i.Owner!.Email
            })
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var viewModel = new UserPageViewModel
        {
            OwnedInventories = ownedInventories,
            SharedInventories = sharedInventories
        };

        return View(viewModel);
    }
}