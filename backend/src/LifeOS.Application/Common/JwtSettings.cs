namespace LifeOS.Application.Common;

/// <summary>
/// Настройки JWT из конфигурации (секция "Jwt").
/// Живут в Application, потому что временем жизни токена управляет
/// бизнес-логика, а не инфраструктура.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>Симметричный ключ подписи. Минимум 32 символа (256 бит для HMAC-SHA256).</summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "LifeOS.API";
    public string Audience { get; set; } = "LifeOS.Client";

    /// <summary>Access-токен намеренно короткоживущий: его нельзя отозвать, только пережить.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh-токен долгоживущий, но отзываемый — он хранится в БД.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
