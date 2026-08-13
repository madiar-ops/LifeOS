using LifeOS.API.Filters;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Health;
using LifeOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Controllers;

/// <summary>Дневник здоровья: одна запись на день.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
[ServiceFilter(typeof(ValidationFilter))]
public class HealthController : ControllerBase
{
    private readonly IHealthLogService _healthLogService;

    public HealthController(IHealthLogService healthLogService) => _healthLogService = healthLogService;

    /// <summary>Записи здоровья за период, свежие — первыми.</summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(PagedResponse<HealthLogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<HealthLogResponse>>> GetAll(
        [FromQuery] HealthLogQueryParams query, CancellationToken cancellationToken)
        => Ok((await _healthLogService.GetAllAsync(query, cancellationToken)).ToResponse());

    /// <summary>Запись по Id.</summary>
    [HttpGet("logs/{id:guid}")]
    [ProducesResponseType(typeof(HealthLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HealthLogResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _healthLogService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Создание записи. На одну дату может быть только одна запись —
    /// повторная попытка вернёт 409.
    /// </summary>
    [HttpPost("logs")]
    [ProducesResponseType(typeof(HealthLogResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HealthLogResponse>> Create(
        [FromBody] CreateHealthLogRequest request, CancellationToken cancellationToken)
    {
        var log = await _healthLogService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = log.Id }, log);
    }

    /// <summary>Обновление записи. Дата не меняется — она часть уникального ключа.</summary>
    [HttpPut("logs/{id:guid}")]
    [ProducesResponseType(typeof(HealthLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HealthLogResponse>> Update(
        Guid id, [FromBody] UpdateHealthLogRequest request, CancellationToken cancellationToken)
        => Ok(await _healthLogService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Удаление записи.</summary>
    [HttpDelete("logs/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _healthLogService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
