using InventoryManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementSystem.Controllers;

public abstract class ApiBaseController : ControllerBase
{
    protected IActionResult HandleServiceResult<T>(ServiceResult<T> result)
    {
        switch (result.ErrorType)
        {
            case ServiceErrorType.None:
                return Ok(result.Data);
            case ServiceErrorType.NotFound:
                return NotFound(new { message = result.ErrorMessage });
            case ServiceErrorType.InvalidInput:
                if (result.ValidationErrors != null)
                {
                    return BadRequest(result.ValidationErrors);
                }
                return BadRequest(new { message = result.ErrorMessage });
            case ServiceErrorType.Forbidden:
                return Forbid();
            case ServiceErrorType.Concurrency:
                return Conflict(new { message = result.ErrorMessage });
            default:
                return StatusCode(500, new { message = result.ErrorMessage ?? "An unexpected error occurred." });
        }
    }
}