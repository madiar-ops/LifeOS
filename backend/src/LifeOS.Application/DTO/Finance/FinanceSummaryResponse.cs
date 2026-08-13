using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Finance;

/// <summary>
/// Агрегаты по финансам за период. Считаются в БД (GroupBy на стороне SQL),
/// а не выгрузкой всех транзакций в память.
/// </summary>
public record FinanceSummaryResponse(
    DateOnly From,
    DateOnly To,
    string Currency,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Balance,
    int TransactionCount,
    IReadOnlyList<CategoryBreakdown> ByCategory);

/// <summary>Разбивка по категориям с долей от общей суммы своего типа.</summary>
public record CategoryBreakdown(
    TransactionType Type,
    string Category,
    decimal Amount,
    decimal Percentage);
