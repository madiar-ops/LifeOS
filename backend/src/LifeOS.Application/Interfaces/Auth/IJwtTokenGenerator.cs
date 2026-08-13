using LifeOS.Domain.Entities;

namespace LifeOS.Application.Interfaces.Auth;

/// <summary>Генерация access-токенов (JWT) и криптостойких refresh-токенов.</summary>
public interface IJwtTokenGenerator
{
    /// <summary>Подписанный JWT + момент его истечения (UTC).</summary>
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);

    /// <summary>
    /// Refresh-токен — это НЕ JWT, а случайная строка.
    /// Его нельзя подделать в принципе: валидность проверяется только по записи в БД.
    /// </summary>
    string GenerateRefreshToken();
}
