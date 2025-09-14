using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IInventoryAccessService _accessService;

    public InventoryController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IInventoryAccessService accessService)
    {
        _context = context;
        _userManager = userManager;
        _accessService = accessService;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("Name", "The Inventory Name field is required.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (userId == null) { return Challenge(); }

        if (ModelState.IsValid)
        {
            var inventory = new Inventory { Name = name, OwnerId = userId };
            _context.Add(inventory);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        ViewData["SubmittedName"] = name;
        return View();
    }

    [AllowAnonymous]
    [HttpGet("Inventory/{id:guid}")]
    public async Task<IActionResult> Index(string id)
    {
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.Id == id);
        if (inventory == null) return NotFound();

        var currentUserId = _userManager.GetUserId(User);
        bool isAdmin = User.IsInRole("Admin");
        bool canManageSettings = !string.IsNullOrEmpty(currentUserId) && _accessService.CanManageSettings(inventory, currentUserId, isAdmin);

        if (canManageSettings)
        {
            ViewData["CanManageSettings"] = true;
            ViewData["CanWrite"] = true;
            ViewData["CurrentUserId"] = currentUserId;
            return View("Manage", inventory);
        }
        else
        {
            bool canWriteItems = !string.IsNullOrEmpty(currentUserId) && await _accessService.CanWrite(inventory, currentUserId, isAdmin);
            ViewData["CanManageSettings"] = false;
            ViewData["CanWrite"] = canWriteItems;
            ViewData["CurrentUserId"] = currentUserId;
            return View("View", inventory);
        }
    }

    [NonAction]
    public IActionResult Manage(string id) => NotFound();

    [NonAction]
    public new IActionResult View(string id) => NotFound();
}