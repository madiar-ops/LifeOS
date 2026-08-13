namespace LifeOS.Application.Interfaces.Infrastructure;

/// <summary>
/// Абстракция над системным временем. Позволяет подменить «сейчас» в unit-тестах
/// (например, проверить истечение refresh-токена без ожидания реального времени).
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}
