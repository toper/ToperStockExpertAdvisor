using Microsoft.AspNetCore.Mvc;

namespace TradingService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
    protected IActionResult InternalServerError(object? value = null)
    {
        return StatusCode(500, value);
    }
}
