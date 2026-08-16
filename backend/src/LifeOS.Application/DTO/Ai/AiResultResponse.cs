namespace LifeOS.Application.DTO.Ai;

/// <summary>
/// Ответ AI в том виде, в каком его получает клиент.
///
/// Поля confidence/isConfident/explanation проходят насквозь до фронтенда:
/// пользователь обязан видеть, насколько модель уверена. Это требование
/// MASTER_GUIDE, и терять его при передаче через backend нельзя.
/// </summary>
public record AiResultResponse<T>(
    T Result,
    decimal Confidence,
    bool IsConfident,
    string Explanation,
    IReadOnlyList<AiContributionResponse> Contributions,
    string ModelVersion);

/// <summary>Вклад признака в результат — основа объяснимости.</summary>
public record AiContributionResponse(string Feature, double Value, double Impact);
