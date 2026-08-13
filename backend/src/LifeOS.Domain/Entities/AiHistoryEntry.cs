using LifeOS.Domain.Common;

namespace LifeOS.Domain.Entities;

/// <summary>
/// Аудит вызовов AI-сервиса: что отправили, что получили, с какой уверенностью.
/// Таблица в БД — "AIHistory". Нужна для отладки, воспроизводимости и демонстрации на защите.
/// </summary>
public class AiHistoryEntry : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Вызванный endpoint AI-сервиса, например "/finance-analysis".</summary>
    public string Endpoint { get; set; } = null!;

    /// <summary>Обезличенный payload запроса (jsonb).</summary>
    public string RequestPayload { get; set; } = "{}";

    /// <summary>Ответ AI-сервиса (jsonb).</summary>
    public string ResponsePayload { get; set; } = "{}";

    public decimal? Confidence { get; set; }

    public User User { get; set; } = null!;
}
