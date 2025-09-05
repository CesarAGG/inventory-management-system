using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InventoryManagementSystem.Controllers;

[ApiController]
public class UtilsController : ControllerBase
{
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
}