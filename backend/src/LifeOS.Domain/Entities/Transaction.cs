using LifeOS.Domain.Common;
using LifeOS.Domain.Enums;

namespace LifeOS.Domain.Entities;

/// <summary>
/// Финансовая операция. Единая таблица для доходов и расходов —
/// различаются полем Type. Заменяет исходную таблицу Expenses.
/// </summary>
public class Transaction : BaseEntity
{
    public Guid UserId { get; set; }

    public TransactionType Type { get; set; }
    public string Category { get; set; } = null!;

    /// <summary>Всегда положительное значение; знак определяется полем Type.</summary>
    public decimal Amount { get; set; }

    /// <summary>Код валюты ISO 4217, например "KZT", "USD".</summary>
    public string Currency { get; set; } = "KZT";

    /// <summary>Дата операции (без времени) — по ней строятся агрегаты Dashboard.</summary>
    public DateOnly Date { get; set; }

    public string? Description { get; set; }

    public User User { get; set; } = null!;
}
