using LifeOS.Application.Ai;

namespace LifeOS.Application.Interfaces.Infrastructure;

/// <summary>
/// Клиент AI-микросервиса. Слой Application не знает про HTTP,
/// внутренний ключ и retry-политику — всё это в Infrastructure.
/// </summary>
public interface IAiService
{
    Task<AiContracts.AiEnvelope<AiContracts.FinanceForecast>> AnalyzeFinanceAsync(
        AiContracts.FinanceAnalysisRequest request, CancellationToken cancellationToken = default);

    Task<AiContracts.AiEnvelope<AiContracts.HealthAssessment>> AnalyzeHealthAsync(
        AiContracts.HealthAnalysisRequest request, CancellationToken cancellationToken = default);

    Task<AiContracts.AiEnvelope<AiContracts.StudySummary>> SummarizeAsync(
        AiContracts.StudySummaryRequest request, CancellationToken cancellationToken = default);

    Task<AiContracts.AiEnvelope<AiContracts.QuizResult>> GenerateQuizAsync(
        AiContracts.QuizRequest request, CancellationToken cancellationToken = default);

    Task<AiContracts.AiEnvelope<AiContracts.ResumeAnalysis>> AnalyzeResumeAsync(
        AiContracts.ResumeAnalysisRequest request, CancellationToken cancellationToken = default);
}
