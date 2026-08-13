using LifeOS.API.Filters;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Tasks;
using LifeOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Controllers;

/// <summary>Задачи пользователя. Могут быть привязаны к цели или самостоятельны.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
[ServiceFilter(typeof(ValidationFilter))]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService) => _taskService = taskService;

    /// <summary>Список задач с фильтрами и пагинацией.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<TaskResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TaskResponse>>> GetAll(
        [FromQuery] TaskQueryParams query, CancellationToken cancellationToken)
        => Ok((await _taskService.GetAllAsync(query, cancellationToken)).ToResponse());

    /// <summary>Задача по Id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _taskService.GetByIdAsync(id, cancellationToken));

    /// <summary>Создание задачи. GoalId необязателен.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> Create(
        [FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await _taskService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    /// <summary>Обновление задачи.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> Update(
        Guid id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
        => Ok(await _taskService.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Переключение статуса выполнения. Отдельный endpoint нужен для чекбокса:
    /// фронту не приходится присылать всю задачу целиком ради одного флага.
    /// </summary>
    [HttpPatch("{id:guid}/complete")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> ToggleComplete(Guid id, CancellationToken cancellationToken)
        => Ok(await _taskService.ToggleCompleteAsync(id, cancellationToken));

    /// <summary>Удаление задачи.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _taskService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
