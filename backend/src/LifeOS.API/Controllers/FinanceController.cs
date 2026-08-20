using LifeOS.API.Extensions;
using LifeOS.API.Filters;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Finance;
using LifeOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeOS.API.Controllers;

/// <summary>Финансы: доходы и расходы в единой таблице транзакций.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
[ServiceFilter(typeof(ValidationFilter))]
public class FinanceController : ControllerBase
{
    private readonly IFinanceService _financeService;

    public FinanceController(IFinanceService financeService) => _financeService = financeService;

    /// <summary>Список транзакций с фильтрами по типу, категории и периоду.</summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PagedResponse<TransactionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> GetAll(
        [FromQuery] TransactionQueryParams query, CancellationToken cancellationToken)
        => Ok((await _financeService.GetAllAsync(query, cancellationToken)).ToResponse());

    /// <summary>Транзакция по Id.</summary>
    [HttpGet("transactions/{id:guid}")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _financeService.GetByIdAsync(id, cancellationToken));

    /// <summary>Создание транзакции (доход или расход — по полю Type).</summary>
    [HttpPost("transactions")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransactionResponse>> Create(
        [FromBody] CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var transaction = await _financeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, transaction);
    }

    /// <summary>Обновление транзакции.</summary>
    [HttpPut("transactions/{id:guid}")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResponse>> Update(
        Guid id, [FromBody] UpdateTransactionRequest request, CancellationToken cancellationToken)
        => Ok(await _financeService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Удаление транзакции.</summary>
    [HttpDelete("transactions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _financeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// AI-прогноз расходов на следующий месяц.
    /// В AI-сервис уходят только помесячные итоги — отдельные транзакции он не видит.
    /// </summary>
    /// <response code="400">Нет данных за период или AI-сервис недоступен.</response>
    [HttpGet("analysis")]
    [EnableRateLimiting(RateLimitingExtensions.AiPolicy)]
    [ProducesResponseType(typeof(AiResultResponse<FinanceForecastResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AiResultResponse<FinanceForecastResponse>>> Analyze(
        [FromQuery] int monthsBack = 6,
        [FromQuery] string? currency = null,
        CancellationToken cancellationToken = default)
        => Ok(await _financeService.AnalyzeAsync(monthsBack, currency, cancellationToken));

    /// <summary>
    /// Сводка за период: доходы, расходы, баланс и разбивка по категориям.
    /// По умолчанию — текущий календарный месяц, валюта KZT.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(FinanceSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FinanceSummaryResponse>> GetSummary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
        => Ok(await _financeService.GetSummaryAsync(from, to, currency, cancellationToken));
}
