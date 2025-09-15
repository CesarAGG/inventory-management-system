using InventoryManagementSystem.Data;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Controllers;

[Authorize]
public class SupportController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly ILogger<SupportController> _logger;

    public SupportController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ICloudStorageService cloudStorageService,
        ILogger<SupportController> logger)
    {
        _userManager = userManager;
        _context = context;
        _cloudStorageService = cloudStorageService;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTicket([FromBody] SupportTicketRequestModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var allowedPriorities = new HashSet<string> { "High", "Average", "Low" };
        if (!allowedPriorities.Contains(model.Priority))
        {
            return BadRequest(new { message = "Invalid priority value." });
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        var adminEmails = admins.Select(a => a.Email).Where(e => !string.IsNullOrEmpty(e)).Select(e => e!).ToList();

        string? inventoryName = null;
        if (!string.IsNullOrEmpty(model.InventoryId))
        {
            var inventory = await _context.Inventories.FindAsync(model.InventoryId);
            inventoryName = inventory?.Name;
        }

        var ticketData = new SupportTicketJsonModel
        {
            ReportedBy = currentUser.Email ?? currentUser.UserName ?? "Unknown User",
            Summary = model.Summary,
            Priority = model.Priority,
            Link = model.SourceUrl,
            Inventory = inventoryName,
            AdminEmails = adminEmails
        };

        var jsonContent = JsonSerializer.Serialize(ticketData, new JsonSerializerOptions { WriteIndented = true });
        var fileName = $"ticket-{DateTime.UtcNow:yyyyMMddHHmmss}-{currentUser.Id}.json";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));

        var fileUrl = await _cloudStorageService.UploadFileAsync(fileName, stream);

        if (string.IsNullOrEmpty(fileUrl))
        {
            _logger.LogError("Failed to upload support ticket for user {UserId}", currentUser.Id);
            return StatusCode(500, new { message = "There was an error submitting your ticket. The issue has been logged." });
        }

        _logger.LogInformation("Successfully created and uploaded support ticket {FileName} for user {UserId}", fileName, currentUser.Id);
        return Ok(new { message = "Support ticket created successfully." });
    }
}