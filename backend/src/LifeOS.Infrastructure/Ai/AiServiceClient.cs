using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeOS.Application.Ai;
using LifeOS.Application.Common;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeOS.Infrastructure.Ai;

/// <summary>
/// Типизированный HttpClient к AI-микросервису.
///
/// Единственное место в системе, знающее про внутренний ключ и адрес FastAPI.
/// React никогда не обращается к AI напрямую — только через этот канал,
/// как требует архитектура (MASTER_GUIDE, раздел ARCHITECTURE).
/// </summary>
public class AiServiceClient : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiServiceClient> _logger;

    /// <summary>
    /// FastAPI отдаёт и принимает snake_case. Настраиваем политику один раз,
    /// вместо того чтобы вешать [JsonPropertyName] на каждое поле контрактов.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AiServiceClient(HttpClient httpClient, ILogger<AiServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<AiContracts.AiEnvelope<AiContracts.FinanceForecast>> AnalyzeFinanceAsync(
        AiContracts.FinanceAnalysisRequest request, CancellationToken cancellationToken = default)
        => PostAsync<AiContracts.FinanceAnalysisRequest, AiContracts.FinanceForecast>(
            "/finance/analysis", request, cancellationToken);

    public Task<AiContracts.AiEnvelope<AiContracts.HealthAssessment>> AnalyzeHealthAsync(
        AiContracts.HealthAnalysisRequest request, CancellationToken cancellationToken = default)
        => PostAsync<AiContracts.HealthAnalysisRequest, AiContracts.HealthAssessment>(
            "/health-analysis", request, cancellationToken);

    public Task<AiContracts.AiEnvelope<AiContracts.StudySummary>> SummarizeAsync(
        AiContracts.StudySummaryRequest request, CancellationToken cancellationToken = default)
        => PostAsync<AiContracts.StudySummaryRequest, AiContracts.StudySummary>(
            "/study/summary", request, cancellationToken);

    public Task<AiContracts.AiEnvelope<AiContracts.QuizResult>> GenerateQuizAsync(
        AiContracts.QuizRequest request, CancellationToken cancellationToken = default)
        => PostAsync<AiContracts.QuizRequest, AiContracts.QuizResult>(
            "/study/quiz", request, cancellationToken);

    public Task<AiContracts.AiEnvelope<AiContracts.ResumeAnalysis>> AnalyzeResumeAsync(
        AiContracts.ResumeAnalysisRequest request, CancellationToken cancellationToken = default)
        => PostAsync<AiContracts.ResumeAnalysisRequest, AiContracts.ResumeAnalysis>(
            "/career/resume-analysis", request, cancellationToken);

    private async Task<AiContracts.AiEnvelope<TResult>> PostAsync<TRequest, TResult>(
        string path, TRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(path, request, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw await BuildFailureAsync(path, response, cancellationToken);

            var envelope = await response.Content
                .ReadFromJsonAsync<AiContracts.AiEnvelope<TResult>>(JsonOptions, cancellationToken);

            return envelope ?? throw new BusinessRuleException(
                "AI-сервис вернул пустой ответ.", "ai.empty_response");
        }
        catch (HttpRequestException ex)
        {
            // Недоступность AI не должна выглядеть как ошибка в коде backend:
            // пользователю нужно понятное сообщение, что функция временно недоступна.
            _logger.LogError(ex, "AI-сервис недоступен при обращении к {Path}", path);

            throw new BusinessRuleException(
                "AI-сервис временно недоступен. Попробуйте позже.", "ai.unavailable");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Отмена не от клиента — значит сработал таймаут HttpClient.
            _logger.LogError(ex, "Таймаут запроса к AI-сервису: {Path}", path);

            throw new BusinessRuleException(
                "AI-сервис не ответил вовремя. Попробуйте позже.", "ai.timeout");
        }
    }

    private async Task<Exception> BuildFailureAsync(
        string path, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogError(
            "AI-сервис вернул {StatusCode} на {Path}: {Body}",
            (int)response.StatusCode, path, Truncate(body, 500));

        return response.StatusCode switch
        {
            // 401 — рассинхрон ключей backend и AI-сервиса. Это ошибка
            // конфигурации развёртывания, а не действий пользователя.
            System.Net.HttpStatusCode.Unauthorized => new BusinessRuleException(
                "Ошибка конфигурации: backend не авторизован в AI-сервисе.", "ai.unauthorized"),

            // 503 — конкретная модель не обучена.
            System.Net.HttpStatusCode.ServiceUnavailable => new BusinessRuleException(
                "Требуемая AI-модель не обучена или недоступна.", "ai.model_unavailable"),

            System.Net.HttpStatusCode.UnprocessableEntity => new BusinessRuleException(
                "AI-сервис отклонил данные запроса.", "ai.invalid_payload"),

            _ => new BusinessRuleException(
                "AI-сервис вернул ошибку при обработке запроса.", "ai.error")
        };
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";
}
