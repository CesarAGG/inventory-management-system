using InventoryManagementSystem.Data;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;

namespace InventoryManagementSystem.Controllers;

[Authorize]
public class UserController : Controller
{
    private readonly ApplicationDbContext _context;

    public UserController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View(new UserPageViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> LoadOwnedInventories([FromForm] DataTablesRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var query = _context.Inventories
            .Where(i => i.OwnerId == userId)
            .AsNoTracking();

        var recordsTotal = await query.CountAsync();

        if (request.Order.Any())
        {
            var order = request.Order.First();
            var sortColumn = order.Column switch
            {
                0 => "Name",
                1 => "CreatedAt",
                2 => "Items.Count",
                3 => "IsPublic",
                _ => "CreatedAt"
            };
            var sortDir = order.Dir?.ToLower() == "desc" ? "desc" : "asc";
            query = query.OrderBy($"{sortColumn} {sortDir}");
        }
        else
        {
            query = query.OrderByDescending(i => i.CreatedAt);
        }

        var pagedData = await query
            .Skip(request.Start)
            .Take(request.Length)
            .Select(i => new InventoryInfoViewModel
            {
                Id = i.Id,
                Name = i.Name,
                CreatedAt = i.CreatedAt,
                IsPublic = i.IsPublic,
                ItemCount = i.Items.Count()
            })
            .ToListAsync();

        var response = new DataTablesResponse<InventoryInfoViewModel>
        {
            Draw = request.Draw,
            RecordsTotal = recordsTotal,
            RecordsFiltered = recordsTotal,
            Data = pagedData
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> LoadSharedInventories([FromForm] DataTablesRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var query = _context.Inventories
            .Where(i => i.Permissions.Any(p => p.UserId == userId && p.Level == Models.PermissionLevel.Write))
            .AsNoTracking();

        var recordsTotal = await query.CountAsync();

        if (request.Order.Any())
        {
            var order = request.Order.First();
            var sortColumn = order.Column switch
            {
                0 => "Name",
                1 => "CreatedAt",
                2 => "Items.Count",
                3 => "Owner.Email",
                _ => "CreatedAt"
            };
            var sortDir = order.Dir?.ToLower() == "desc" ? "desc" : "asc";
            query = query.OrderBy($"{sortColumn} {sortDir}");
        }
        else
        {
            query = query.OrderByDescending(i => i.CreatedAt);
        }

        var pagedData = await query
            .Skip(request.Start)
            .Take(request.Length)
            .Select(i => new InventoryInfoViewModel
            {
                Id = i.Id,
                Name = i.Name,
                CreatedAt = i.CreatedAt,
                ItemCount = i.Items.Count(),
                OwnerEmail = i.Owner!.Email
            })
            .ToListAsync();

        var response = new DataTablesResponse<InventoryInfoViewModel>
        {
            Draw = request.Draw,
            RecordsTotal = recordsTotal,
            RecordsFiltered = recordsTotal,
            Data = pagedData
        };

        return Ok(response);
    }
}