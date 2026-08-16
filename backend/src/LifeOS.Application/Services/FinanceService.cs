using AutoMapper;
using LifeOS.Application.Ai;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Finance;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using LifeOS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Application.Services;

public class FinanceService : IFinanceService
{
    private const string DefaultCurrency = "KZT";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IAiService _ai;
    private readonly IAiHistoryRecorder _history;
    private readonly IMapper _mapper;

    public FinanceService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IAiService ai,
        IAiHistoryRecorder history,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _ai = ai;
        _history = history;
        _mapper = mapper;
    }

    public async Task<PagedResult<TransactionResponse>> GetAllAsync(
        TransactionQueryParams query, CancellationToken cancellationToken = default)
    {
        var source = BuildQuery(query.Type, query.Category, query.From, query.To, null);

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Transaction>(items, totalCount, query.PageNumber, query.PageSize)
            .Map(_mapper.Map<TransactionResponse>);
    }

    public async Task<TransactionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _mapper.Map<TransactionResponse>(await LoadOwnedAsync(id, tracked: false, cancellationToken));

    public async Task<TransactionResponse> CreateAsync(
        CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = new Transaction
        {
            UserId = _currentUser.GetRequiredUserId(),
            Type = request.Type,
            Category = request.Category.Trim(),
            // Знак операции несёт поле Type, поэтому сумма всегда положительна.
            Amount = Math.Abs(request.Amount),
            Currency = NormalizeCurrency(request.Currency),
            Date = request.Date,
            Description = request.Description?.Trim()
        };

        await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TransactionResponse>(transaction);
    }

    public async Task<TransactionResponse> UpdateAsync(
        Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await LoadOwnedAsync(id, tracked: true, cancellationToken);

        transaction.Type = request.Type;
        transaction.Category = request.Category.Trim();
        transaction.Amount = Math.Abs(request.Amount);
        transaction.Currency = NormalizeCurrency(request.Currency);
        transaction.Date = request.Date;
        transaction.Description = request.Description?.Trim();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TransactionResponse>(transaction);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await LoadOwnedAsync(id, tracked: true, cancellationToken);

        _unitOfWork.Transactions.Remove(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Агрегаты за период. Группировка выполняется на стороне PostgreSQL:
    /// в память попадают только строки итогов, а не все транзакции пользователя.
    ///
    /// Суммирование идёт в рамках ОДНОЙ валюты — конвертации по курсам в MVP нет,
    /// поэтому смешивать KZT и USD в одну сумму было бы враньём в отчёте.
    /// </summary>
    public async Task<FinanceSummaryResponse> GetSummaryAsync(
        DateOnly? from, DateOnly? to, string? currency, CancellationToken cancellationToken = default)
    {
        var today = _dateTime.Today;

        // По умолчанию — текущий календарный месяц.
        var periodFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var periodTo = to ?? today;
        var targetCurrency = NormalizeCurrency(currency ?? DefaultCurrency);

        var source = BuildQuery(null, null, periodFrom, periodTo, targetCurrency);

        var byCategory = await source
            .GroupBy(t => new { t.Type, t.Category })
            .Select(g => new
            {
                g.Key.Type,
                g.Key.Category,
                Amount = g.Sum(t => t.Amount),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var totalIncome = byCategory
            .Where(x => x.Type == TransactionType.Income)
            .Sum(x => x.Amount);

        var totalExpense = byCategory
            .Where(x => x.Type == TransactionType.Expense)
            .Sum(x => x.Amount);

        var breakdown = byCategory
            .Select(x =>
            {
                // Доля считается от суммы СВОЕГО типа: 30% расходов на еду —
                // это 30% от расходов, а не от оборота.
                var typeTotal = x.Type == TransactionType.Income ? totalIncome : totalExpense;
                var percentage = typeTotal == 0 ? 0 : Math.Round(x.Amount / typeTotal * 100, 2);

                return new CategoryBreakdown(x.Type, x.Category, x.Amount, percentage);
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        return new FinanceSummaryResponse(
            periodFrom,
            periodTo,
            targetCurrency,
            totalIncome,
            totalExpense,
            totalIncome - totalExpense,
            byCategory.Sum(x => x.Count),
            breakdown);
    }

    /// <summary>
    /// AI-прогноз расходов. Backend сам агрегирует историю по месяцам
    /// и передаёт в AI-сервис только итоги — тот не имеет доступа к БД
    /// и не должен видеть отдельные транзакции пользователя.
    /// </summary>
    public async Task<AiResultResponse<FinanceForecastResponse>> AnalyzeAsync(
        int monthsBack, string? currency, CancellationToken cancellationToken = default)
    {
        var targetCurrency = NormalizeCurrency(currency ?? DefaultCurrency);
        var today = _dateTime.Today;

        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-Math.Max(monthsBack - 1, 0));

        var monthly = await BuildQuery(null, null, from, today, targetCurrency)
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => (decimal?)t.Amount) ?? 0m,
                Expense = g.Where(t => t.Type == TransactionType.Expense).Sum(t => (decimal?)t.Amount) ?? 0m
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        if (monthly.Count == 0)
            throw new BusinessRuleException(
                "Недостаточно данных для прогноза: за выбранный период нет ни одной операции.",
                "finance.no_data");

        var history = monthly
            .Select(x => new AiContracts.MonthlyTotal($"{x.Year:D4}-{x.Month:D2}", x.Income, x.Expense))
            .ToList();

        var categories = await BuildQuery(TransactionType.Expense, null, from, today, targetCurrency)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Amount = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Amount)
            .Take(10)
            .ToListAsync(cancellationToken);

        var request = new AiContracts.FinanceAnalysisRequest(
            history,
            categories.Select(c => new AiContracts.CategoryTotal(c.Category, c.Amount)).ToList(),
            targetCurrency);

        var envelope = await _ai.AnalyzeFinanceAsync(request, cancellationToken);
        var forecast = envelope.Result;

        await _history.RecordAsync(
            "/finance/analysis",
            new { monthsAnalyzed = history.Count, currency = targetCurrency },
            forecast,
            envelope.Confidence,
            envelope.IsConfident,
            ModuleType.Finance,
            recommendationText: BuildRecommendation(forecast, targetCurrency),
            cancellationToken);

        var response = new FinanceForecastResponse(
            forecast.PredictedExpense,
            forecast.PredictedBalance,
            forecast.Trend,
            forecast.TopCategory,
            forecast.SavingsRate,
            targetCurrency,
            history.Count);

        return envelope.ToResponse(response);
    }

    // ---- Вспомогательные методы -----------------------------------------

    private static string? BuildRecommendation(AiContracts.FinanceForecast forecast, string currency)
    {
        if (forecast.PredictedBalance < 0)
            return $"Прогноз показывает дефицит около {Math.Abs(forecast.PredictedBalance):N0} {currency} " +
                   "в следующем месяце. Стоит пересмотреть крупные траты.";

        if (forecast.Trend == "rising" && forecast.TopCategory is not null)
            return $"Расходы растут, больше всего уходит на категорию «{forecast.TopCategory}». " +
                   "Проверьте, нет ли там необязательных трат.";

        if (forecast.SavingsRate >= 0.20m)
            return $"Удаётся откладывать около {forecast.SavingsRate * 100:0}% дохода — " +
                   "хороший момент, чтобы задать финансовую цель.";

        return null;
    }

    private static string NormalizeCurrency(string currency)
        => string.IsNullOrWhiteSpace(currency)
            ? DefaultCurrency
            : currency.Trim().ToUpperInvariant();

    private IQueryable<Transaction> BuildQuery(
        TransactionType? type, string? category, DateOnly? from, DateOnly? to, string? currency)
    {
        var userId = _currentUser.GetRequiredUserId();

        var source = _unitOfWork.Transactions.Query().Where(t => t.UserId == userId);

        if (type.HasValue) source = source.Where(t => t.Type == type.Value);
        if (from.HasValue) source = source.Where(t => t.Date >= from.Value);
        if (to.HasValue) source = source.Where(t => t.Date <= to.Value);

        if (!string.IsNullOrWhiteSpace(category))
            source = source.Where(t => t.Category == category.Trim());

        if (!string.IsNullOrWhiteSpace(currency))
            source = source.Where(t => t.Currency == currency);

        return source;
    }

    private async Task<Transaction> LoadOwnedAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var transaction = await _unitOfWork.Transactions.Query(asNoTracking: !tracked)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(
            transaction, transaction?.UserId ?? Guid.Empty, userId, nameof(Transaction), id);
    }
}
