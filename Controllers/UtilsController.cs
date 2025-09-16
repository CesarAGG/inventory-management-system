using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using InventoryManagementSystem.Services;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagementSystem.Controllers;

[ApiController]
public class UtilsController : ControllerBase
{
    private readonly ICloudStorageService? _cloudStorageService;

    // A parameterless constructor for when the service is not needed
    public UtilsController() { }

    [ActivatorUtilitiesConstructor]
    public UtilsController(ICloudStorageService cloudStorageService)
    {
        _cloudStorageService = cloudStorageService;
    }

    [AllowAnonymous]
    [HttpPost("api/utils/date-previews")]
    public IActionResult PostDatePreviews([FromBody] string[] formats)
    {
        if (formats == null || formats.Length == 0)
        {
            return Ok(new List<object>());
        }

        var previews = formats.Select(format =>
        {
            if (string.IsNullOrEmpty(format))
            {
                return new { format, preview = "[Empty Format]", isValid = false };
            }
            try
            {
                var preview = DateTime.UtcNow.ToString(format);
                return new { format, preview, isValid = true };
            }
            catch (FormatException)
            {
                return new { format, preview = "[Invalid Format]", isValid = false };
            }
        }).ToList();

        return Ok(previews);
    }

#if DEBUG
    // THIS IS A TEMPORARY ENDPOINT FOR VERIFICATION AND MUST BE REMOVED
    // AT THE END OF THE SPRINT BEFORE ANY PRODUCTION DEPLOYMENT.
    [HttpGet("api/utils/test-upload")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TestUpload()
    {
        if (_cloudStorageService == null)
        {
             return BadRequest(new { message = "CloudStorageService not available." });
        }
        var content = $"This is a test file generated at {DateTime.UtcNow:o}";
        var fileName = $"test-ticket-{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var fileUrl = await _cloudStorageService.UploadFileAsync(fileName, stream);

        if (string.IsNullOrEmpty(fileUrl))
        {
            return BadRequest(new { message = "File upload failed. Check logs for details." });
        }

        return Ok(new { message = "File uploaded successfully!", url = fileUrl });
    }
#endif
}