using LifeOS.Application.Interfaces.Auth;

namespace LifeOS.Infrastructure.Auth;

/// <summary>
/// Хеширование через BCrypt. Соль генерируется автоматически и хранится
/// внутри самого хеша — отдельное поле в БД не нужно.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Work factor 12: примерно 0.2–0.3 с на хеш на обычном железе.
    /// Достаточно медленно для перебора и достаточно быстро для живого логина.
    /// </summary>
    private const int WorkFactor = 12;

    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Хеш в БД повреждён или сохранён другим алгоритмом.
            // Считаем это неудачной проверкой, а не падением приложения.
            return false;
        }
    }
}
