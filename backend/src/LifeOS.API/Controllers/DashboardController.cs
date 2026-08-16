using LifeOS.Application.DTO.Dashboard;
using LifeOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Controllers;

/// <summary>Главный экран: сводка по всем модулям одним запросом.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
        => _dashboardService = dashboardService;

    /// <summary>
    /// Сводка за период: цели, задачи, финансы, здоровье, учёба, карьера,
    /// свежие рекомендации и последние файлы.
    ///
    /// Вызовов AI здесь нет — экран обязан открываться мгновенно.
    /// Рекомендации берутся из тех, что уже посчитаны при явных запросах анализа.
    /// </summary>
    /// <param name="days">Глубина периода в днях, 1–365. По умолчанию 30.</param>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardResponse>> Get(
        [FromQuery] int days = 30, CancellationToken cancellationToken = default)
        => Ok(await _dashboardService.GetAsync(days, cancellationToken));
}
