namespace LifeOS.Application.Interfaces.Auth;

/// <summary>
/// Хеширование паролей. Абстракция нужна, чтобы алгоритм (сейчас BCrypt)
/// можно было заменить, не трогая AuthService.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Проверка пароля против хеша. Устойчива к timing-атакам (реализация BCrypt).</summary>
    bool Verify(string password, string passwordHash);
}
