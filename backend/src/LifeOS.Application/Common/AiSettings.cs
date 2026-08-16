namespace LifeOS.Application.Common;

/// <summary>
/// Настройки канала к AI-микросервису (секция "AiService").
/// InternalApiKey обязан совпадать с INTERNAL_API_KEY в ai-service/.env —
/// это один и тот же общий секрет.
/// </summary>
public class AiSettings
{
    public const string SectionName = "AiService";

    public string BaseUrl { get; set; } = "http://localhost:8000";

    public string InternalApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Таймаут запроса. Больше стандартных 100 секунд не нужно: суммаризация
    /// длинного PDF через LLM — самая медленная операция, и она укладывается.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 90;

    /// <summary>
    /// Минимальная уверенность, при которой ответ AI сохраняется
    /// как рекомендация пользователю. Ниже — результат показывается,
    /// но в ленту рекомендаций не попадает.
    /// </summary>
    public decimal RecommendationThreshold { get; set; } = 0.60m;
}
