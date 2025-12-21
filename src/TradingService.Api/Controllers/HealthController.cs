using Microsoft.AspNetCore.Mvc;

namespace TradingService.Api.Controllers;

public class HealthController : BaseController
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "TradingService.Api",
            Timestamp = DateTime.UtcNow
        });
    }
}
