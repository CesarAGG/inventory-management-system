using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Security.Claims;

namespace InventoryManagementSystem.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public InventoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: /Inventory/Create
    public IActionResult Create()
    {
        return View();
    }

    // GET: /Inventory/Manage/{id}
    [HttpGet]
    public async Task<IActionResult> Manage(string id)
    {
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.Id == id);

        if (inventory == null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inventory.OwnerId != currentUserId && !User.IsInRole("Admin"))
        {
            // If the user is not the owner and not an Admin, deny access.
            return Forbid();
        }

        return View(inventory);
    }

    // POST: /Inventory/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("Name", "The Inventory Name field is required.");
        }

        var userId = _userManager.GetUserId(User);
        if (userId == null) { return Challenge(); }

        if (ModelState.IsValid)
        {
            var inventory = new Inventory
            {
                Name = name,
                OwnerId = userId
            };

            _context.Add(inventory);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        // If we got this far, something failed, redisplay form
        ViewData["SubmittedName"] = name;
        return View();
    }
}