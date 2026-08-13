using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Controllers;

/// <summary>
/// Служебная проверка живости API.
/// Раньше жил на /api/health — переехал на /api/ping, чтобы освободить
/// маршрут /api/health для одноимённого модуля LifeOS.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PingController : ControllerBase
{
    /// <summary>Проверка, что API поднят.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new
    {
        service = "LifeOS.API",
        status = "healthy",
        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
        utcTime = DateTime.UtcNow
    });
}
