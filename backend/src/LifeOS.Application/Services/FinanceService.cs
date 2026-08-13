using AutoMapper;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Finance;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Application.Services;

public class FinanceService : IFinanceService
{
    private const string DefaultCurrency = "KZT";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IMapper _mapper;

    public FinanceService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _dateTime = dateTime;
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

    // ---- Вспомогательные методы -----------------------------------------

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
