namespace LifeOS.Domain.Exceptions;

/// <summary>
/// Базовое исключение бизнес-правил домена.
/// Все наследники перехватываются GlobalExceptionMiddleware и превращаются
/// в корректный HTTP-ответ (ProblemDetails). Контроллеры не ловят их вручную.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }

    /// <summary>Машиночитаемый код ошибки для фронтенда (например, "user.email_taken").</summary>
    public abstract string Code { get; }
}
