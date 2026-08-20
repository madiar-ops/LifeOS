using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LifeOS.Application.Ai;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Finance;
using LifeOS.Domain.Enums;
using LifeOS.IntegrationTests.Infrastructure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LifeOS.IntegrationTests.Scenarios;

/// <summary>
/// Взаимодействие backend с AI-сервисом.
///
/// Сам FastAPI здесь подменён: его собственное поведение проверяется в pytest.
/// Проверяется стык — три вещи, которые ломаются именно на границе сервисов:
/// уверенность модели доходит до клиента без потерь, недостаток данных
/// отличается от аварии, а отдельные транзакции пользователя за пределы
/// backend не уходят.
/// </summary>
[Collection(ApiCollection.Name)]
public class AiIntegrationTests
{
    private readonly ApiFixture _api;

    public AiIntegrationTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Уверенность_модели_доходит_до_клиента()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        await CreateTransactionAsync(user, 45_000m, "Продукты");

        _api.GivenAiReturnsFinanceForecast(confidence: 0.87m, isConfident: true);

        var response = await user.Client.GetAsync("/api/finance/analysis");
        var result = await response.Content
            .ReadFromJsonAsync<AiResultResponse<FinanceForecastResponse>>(ApiFixture.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Требование MASTER_GUIDE: пользователь обязан видеть, насколько
        // модель уверена. Потеря этих полей при проходе через backend
        // нарушила бы его молча — ответ выглядел бы совершенно нормально.
        result!.Confidence.Should().Be(0.87m);
        result.IsConfident.Should().BeTrue();
        result.Explanation.Should().NotBeNullOrWhiteSpace();
        result.ModelVersion.Should().Be("finance-gbr-test");
        result.Contributions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Низкая_уверенность_доходит_как_есть_а_не_превращается_в_ошибку()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        await CreateTransactionAsync(user, 12_000m, "Кафе");

        _api.GivenAiReturnsFinanceForecast(confidence: 0.22m, isConfident: false);

        var result = await user.Client.GetFromJsonAsync<AiResultResponse<FinanceForecastResponse>>(
            "/api/finance/analysis", ApiFixture.Json);

        // Неуверенный ответ — это тоже ответ. Скрыть его значило бы лишить
        // пользователя информации; показать без пометки — обмануть его.
        result!.IsConfident.Should().BeFalse();
        result.Confidence.Should().Be(0.22m);
    }

    [Fact]
    public async Task Отсутствие_данных_отличается_от_аварии_отдельным_кодом()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.GetAsync("/api/finance/analysis");

        // 400 с кодом finance.no_data, а не 500: интерфейс покажет подсказку
        // «добавьте операции», а не сообщение об ошибке сервера.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemReader.ReadAsync(response)).Code.Should().Be("finance.no_data");
    }

    [Fact]
    public async Task В_AI_сервис_уходят_только_помесячные_итоги()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        await CreateTransactionAsync(user, 33_000m, "Продукты", description: "чек №4417, ТОО «Магнум»");

        _api.GivenAiReturnsFinanceForecast();

        await user.Client.GetAsync("/api/finance/analysis");

        var sent = _api.Ai.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault())
            .OfType<AiContracts.FinanceAnalysisRequest>()
            .Last();

        // Внешнему сервису передаются агрегаты, а не выписка по счёту.
        // Описания операций, даты и идентификаторы наружу не уходят —
        // это осознанное ограничение объёма передаваемых данных.
        sent.History.Should().NotBeEmpty();
        sent.History.Should().OnlyContain(month => month.Month.Length == 7, "формат ГГГГ-ММ");
        sent.Categories.Should().OnlyContain(category => category.Category == "Продукты");
        sent.Currency.Should().Be("KZT");
    }

    [Fact]
    public async Task Недоступность_AI_сервиса_не_роняет_приложение()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        await CreateTransactionAsync(user, 5_000m, "Транспорт");

        _api.Ai
            .AnalyzeFinanceAsync(Arg.Any<AiContracts.FinanceAnalysisRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("AI-сервис не отвечает"));

        var response = await user.Client.GetAsync("/api/finance/analysis");

        // Приложение обязано ответить осмысленным кодом, а не оборвать
        // соединение: недоступность соседнего сервиса — штатная ситуация.
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await ProblemReader.ReadAsync(response);
        problem.Code.Should().NotBeNullOrEmpty();
        problem.TraceId.Should().NotBeNullOrEmpty();

        // Возвращаем заглушку в исходное состояние, чтобы не влиять
        // на другие тесты, использующие общий экземпляр.
        _api.GivenAiReturnsFinanceForecast();
    }

    [Fact]
    public async Task Успешный_анализ_попадает_в_историю_обращений_к_AI()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        await CreateTransactionAsync(user, 27_000m, "Продукты");

        _api.GivenAiReturnsFinanceForecast(confidence: 0.91m, isConfident: true);
        await user.Client.GetAsync("/api/finance/analysis");

        var history = await user.Client.GetFromJsonAsync<PagedResponse<AiHistoryResponse>>(
            "/api/ai/history", ApiFixture.Json);

        // Аудит обращений к AI — требование объяснимости: по нему видно,
        // какой ответ модель дала пользователю и когда.
        history!.Items.Should().NotBeEmpty();
        history.Items.Should().Contain(entry => entry.Endpoint.Contains("finance"));
    }

    private static async Task CreateTransactionAsync(
        TestUser user, decimal amount, string category, string? description = null)
    {
        var response = await user.Client.PostAsJsonAsync(
            "/api/finance/transactions",
            new CreateTransactionRequest(
                TransactionType.Expense,
                category,
                amount,
                "KZT",
                DateOnly.FromDateTime(DateTime.UtcNow),
                description),
            ApiFixture.Json);

        response.EnsureSuccessStatusCode();
    }
}
