using InventoryManagementSystem.Data;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using InventoryManagementSystem.Models;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;

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

        var projection = (Expression<Func<Inventory, InventoryInfoViewModel>>)(i => new InventoryInfoViewModel
        {
            Id = i.Id,
            Name = i.Name,
            CreatedAt = i.CreatedAt,
            IsPublic = i.IsPublic,
            ItemCount = i.Items.Count()
        });

        return await LoadInventoriesForDataTable(query, request, projection);
    }

    [HttpPost]
    public async Task<IActionResult> LoadSharedInventories([FromForm] DataTablesRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var query = _context.Inventories
            .Where(i => i.Permissions.Any(p => p.UserId == userId && p.Level == Models.PermissionLevel.Write))
            .AsNoTracking();

        var projection = (Expression<Func<Inventory, InventoryInfoViewModel>>)(i => new InventoryInfoViewModel
        {
            Id = i.Id,
            Name = i.Name,
            CreatedAt = i.CreatedAt,
            ItemCount = i.Items.Count(),
            OwnerEmail = i.Owner!.Email
        });

        return await LoadInventoriesForDataTable(query, request, projection);
    }

    private async Task<IActionResult> LoadInventoriesForDataTable(
        IQueryable<Inventory> query,
        DataTablesRequest request,
        Expression<Func<Inventory, InventoryInfoViewModel>> projection)
    {
        var recordsTotal = await query.CountAsync();

        if (request.Order.Any())
        {
            var order = request.Order.First();
            var isDescending = order.Dir?.ToLower() == "desc";

            Expression<Func<Inventory, object>> sortColumnExpression = order.Column switch
            {
                0 => i => i.Name,
                2 => i => i.Items.Count(),
                3 => i => i.Owner!.Email!, 
                _ => i => i.CreatedAt
            };

            query = isDescending
                ? query.OrderByDescending(sortColumnExpression)
                : query.OrderBy(sortColumnExpression);
        }
        else
        {
            query = query.OrderByDescending(i => i.CreatedAt);
        }

        var pagedData = await query
            .Skip(request.Start)
            .Take(request.Length)
            .Select(projection)
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