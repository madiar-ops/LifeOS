namespace LifeOS.Application.Common;

/// <summary>Машиночитаемая ошибка операции: код для фронтенда + человекочитаемое сообщение.</summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string message) => new("resource.not_found", message);
    public static Error Conflict(string message) => new("resource.conflict", message);
    public static Error Validation(string message) => new("validation.failed", message);
    public static Error Forbidden(string message) => new("access.forbidden", message);
}
