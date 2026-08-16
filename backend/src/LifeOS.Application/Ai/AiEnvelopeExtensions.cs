using LifeOS.Application.DTO.Ai;

namespace LifeOS.Application.Ai;

public static class AiEnvelopeExtensions
{
    /// <summary>
    /// Переносит метаданные ответа AI (уверенность, объяснение, вклад признаков)
    /// из внутреннего контракта в DTO для клиента.
    ///
    /// Вынесено в общий метод, потому что этот перенос обязан быть одинаковым
    /// во всех модулях: потеря confidence хотя бы в одном месте нарушила бы
    /// требование MASTER_GUIDE об обязательной оценке уверенности.
    /// </summary>
    public static AiResultResponse<TResult> ToResponse<TResult, TAi>(
        this AiContracts.AiEnvelope<TAi> envelope, TResult result)
        => new(
            result,
            envelope.Confidence,
            envelope.IsConfident,
            envelope.Explanation,
            (envelope.Contributions ?? new List<AiContracts.FeatureContribution>())
                .Select(c => new AiContributionResponse(c.Feature, c.Value, c.Impact))
                .ToList(),
            envelope.ModelVersion);
}
