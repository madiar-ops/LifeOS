using LifeOS.API.Filters;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Goals;
using LifeOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Controllers;

/// <summary>Цели пользователя.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
[ServiceFilter(typeof(ValidationFilter))]
public class GoalsController : ControllerBase
{
    private readonly IGoalService _goalService;

    public GoalsController(IGoalService goalService) => _goalService = goalService;

    /// <summary>Список целей с фильтрами и пагинацией.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<GoalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<GoalResponse>>> GetAll(
        [FromQuery] GoalQueryParams query, CancellationToken cancellationToken)
        => Ok((await _goalService.GetAllAsync(query, cancellationToken)).ToResponse());

    /// <summary>Цель по Id вместе со статистикой по задачам.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GoalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoalResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _goalService.GetByIdAsync(id, cancellationToken));

    /// <summary>Создание цели.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(GoalResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GoalResponse>> Create(
        [FromBody] CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var goal = await _goalService.CreateAsync(request, cancellationToken);

        // 201 + заголовок Location на созданный ресурс — как требует REST.
        return CreatedAtAction(nameof(GetById), new { id = goal.Id }, goal);
    }

    /// <summary>Обновление цели.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(GoalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoalResponse>> Update(
        Guid id, [FromBody] UpdateGoalRequest request, CancellationToken cancellationToken)
        => Ok(await _goalService.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Удаление цели. Задачи цели не удаляются — у них GoalId станет NULL.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _goalService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
