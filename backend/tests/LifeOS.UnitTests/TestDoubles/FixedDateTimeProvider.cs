using LifeOS.Application.Interfaces.Infrastructure;

namespace LifeOS.UnitTests.TestDoubles;

/// <summary>
/// Провайдер времени с фиксированным «сейчас».
///
/// Ради него в Фазе 1 и вводился <see cref="IDateTimeProvider"/>: срок жизни
/// токена проверяется сдвигом переменной, а не ожиданием семи суток.
/// </summary>
public sealed class FixedDateTimeProvider : IDateTimeProvider
{
    public FixedDateTimeProvider(DateTime utcNow) => UtcNow = utcNow;

    /// <summary>Момент «сейчас». Тест может двигать его вперёд и назад.</summary>
    public DateTime UtcNow { get; set; }

    public DateOnly Today => DateOnly.FromDateTime(UtcNow);

    /// <summary>Фиксированная точка отсчёта, от которой считают все тесты аутентификации.</summary>
    public static FixedDateTimeProvider Default =>
        new(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
}
