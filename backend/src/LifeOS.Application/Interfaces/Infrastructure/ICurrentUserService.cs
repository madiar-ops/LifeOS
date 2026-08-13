using LifeOS.Domain.Enums;

namespace LifeOS.Application.Interfaces.Infrastructure;

/// <summary>
/// Данные текущего пользователя, извлечённые из JWT.
/// Слой Application не знает про HttpContext — только про этот интерфейс.
/// Реализация появится в Фазе 2 (Auth) и будет читать ClaimsPrincipal.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }

    /// <summary>UserId или исключение — для мест, где аноним невозможен.</summary>
    Guid GetRequiredUserId();
}
