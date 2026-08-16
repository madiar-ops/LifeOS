using LifeOS.Application.Common;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Controllers;

/// <summary>Рекомендации AI и аудит обращений к AI-сервису.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;

    public RecommendationsController(IRecommendationService recommendationService)
        => _recommendationService = recommendationService;

    /// <summary>
    /// Лента рекомендаций. Сюда попадают только те выводы AI,
    /// в которых модель была достаточно уверена.
    /// </summary>
    [HttpGet("recommendations")]
    [ProducesResponseType(typeof(PagedResponse<RecommendationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<RecommendationResponse>>> GetAll(
        [FromQuery] PaginationParams pagination,
        [FromQuery] ModuleType? module,
        CancellationToken cancellationToken)
        => Ok((await _recommendationService.GetAllAsync(pagination, module, cancellationToken)).ToResponse());

    /// <summary>Скрыть рекомендацию.</summary>
    [HttpDelete("recommendations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _recommendationService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// История обращений к AI. Содержимое запросов наружу не отдаётся —
    /// там могут быть фрагменты личных документов.
    /// </summary>
    [HttpGet("ai/history")]
    [ProducesResponseType(typeof(PagedResponse<AiHistoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AiHistoryResponse>>> GetHistory(
        [FromQuery] PaginationParams pagination, CancellationToken cancellationToken)
        => Ok((await _recommendationService.GetHistoryAsync(pagination, cancellationToken)).ToResponse());
}
