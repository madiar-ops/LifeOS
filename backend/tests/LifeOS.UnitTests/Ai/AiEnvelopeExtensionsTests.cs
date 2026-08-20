using FluentAssertions;
using LifeOS.Application.Ai;
using LifeOS.Application.DTO.Ai;

namespace LifeOS.UnitTests.Ai;

/// <summary>
/// Тесты переноса метаданных AI в ответ клиенту.
///
/// MASTER_GUIDE требует: «если AI не уверен — он сообщает об этом».
/// Технически это требование выполняется ровно здесь: единственный метод,
/// через который проходят ответы всех AI-модулей. Потеря confidence в нём
/// нарушила бы требование сразу во всём приложении и незаметно.
/// </summary>
public class AiEnvelopeExtensionsTests
{
    private static AiContracts.AiEnvelope<string> Envelope(
        decimal confidence = 0.82m,
        bool isConfident = true,
        List<AiContracts.FeatureContribution>? contributions = null)
        => new(
            "сырой результат модели",
            confidence,
            isConfident,
            "Прогноз построен по 6 месяцам истории.",
            contributions,
            "finance-gbr-1.2.0");

    [Fact]
    public void Уверенность_и_объяснение_доходят_до_клиента_без_изменений()
    {
        var response = Envelope().ToResponse(new { Predicted = 120_000m });

        response.Confidence.Should().Be(0.82m);
        response.IsConfident.Should().BeTrue();
        response.Explanation.Should().Be("Прогноз построен по 6 месяцам истории.");
        response.ModelVersion.Should().Be("finance-gbr-1.2.0");
    }

    [Fact]
    public void Признак_уверенности_считает_сервер_а_не_клиент()
    {
        // Порог доверия задан настройкой AiService:RecommendationThreshold
        // и применяется на стороне AI-сервиса. Клиент получает готовый флаг:
        // двух мнений о том, можно ли верить модели, быть не должно.
        var response = Envelope(confidence: 0.31m, isConfident: false).ToResponse("результат");

        response.Confidence.Should().Be(0.31m);
        response.IsConfident.Should().BeFalse();
    }

    [Fact]
    public void Отсутствие_вклада_признаков_даёт_пустой_список_а_не_null()
    {
        var response = Envelope(contributions: null).ToResponse("результат");

        // null дошёл бы до JSON как `"contributions": null`, и фронтенду
        // пришлось бы отдельно проверять это перед перебором.
        response.Contributions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Вклад_признаков_переносится_целиком_и_в_том_же_порядке()
    {
        var contributions = new List<AiContracts.FeatureContribution>
        {
            new("Расходы на еду", 45_000, 0.42),
            new("Средний чек", 3_200, 0.31),
            new("Число операций", 84, 0.27)
        };

        var response = Envelope(contributions: contributions).ToResponse("результат");

        response.Contributions.Should().HaveCount(3);
        response.Contributions.Select(c => c.Feature)
                .Should().Equal("Расходы на еду", "Средний чек", "Число операций");
        response.Contributions[0].Value.Should().Be(45_000);
        response.Contributions[0].Impact.Should().Be(0.42);
    }

    [Fact]
    public void Результат_подставляется_тот_который_передали()
    {
        var forecast = new FinanceForecastResponse(
            PredictedExpense: 118_400m,
            PredictedBalance: 31_600m,
            Trend: "rising",
            TopCategory: "Продукты",
            SavingsRate: 0.21m,
            Currency: "KZT",
            MonthsAnalyzed: 6);

        var response = Envelope().ToResponse(forecast);

        response.Result.Should().BeSameAs(forecast);
        response.Result.PredictedExpense.Should().Be(118_400m);
    }

    [Fact]
    public void Нулевая_уверенность_остаётся_нулевой_а_не_превращается_в_отсутствие_данных()
    {
        // Ответ с confidence = 0 — это честный ответ «модель не смогла»,
        // и он обязан дойти до пользователя именно в таком виде.
        var response = Envelope(confidence: 0m, isConfident: false).ToResponse("результат");

        response.Confidence.Should().Be(0m);
        response.IsConfident.Should().BeFalse();
    }
}
