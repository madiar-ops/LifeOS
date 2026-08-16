namespace LifeOS.Application.Ai;

/// <summary>
/// Контракты обмена с AI-сервисом.
///
/// Это ОТДЕЛЬНЫЕ типы, а не общие с доменом: FastAPI — независимый сервис
/// со своим жизненным циклом. Если его схема изменится, сломается ровно
/// один файл, а не доменная модель.
///
/// Имена полей в snake_case — как в Python. Преобразование задаётся
/// политикой сериализации в AiServiceClient.
/// </summary>
public static class AiContracts
{
    // ---- Общая обёртка ответа ---------------------------------------

    public record FeatureContribution(string Feature, double Value, double Impact);

    public record AiEnvelope<T>(
        T Result,
        decimal Confidence,
        bool IsConfident,
        string Explanation,
        List<FeatureContribution>? Contributions,
        string ModelVersion);

    // ---- Finance -----------------------------------------------------

    public record MonthlyTotal(string Month, decimal Income, decimal Expense);

    public record CategoryTotal(string Category, decimal Amount);

    public record FinanceAnalysisRequest(
        List<MonthlyTotal> History,
        List<CategoryTotal> Categories,
        string Currency);

    public record FinanceForecast(
        decimal PredictedExpense,
        decimal PredictedBalance,
        string Trend,
        string? TopCategory,
        decimal SavingsRate);

    // ---- Health ------------------------------------------------------

    public record HealthEntry(
        string Date,
        decimal? SleepHours,
        int WaterMl,
        int Steps,
        decimal? Weight,
        int? Mood);

    public record HealthAnalysisRequest(List<HealthEntry> Entries);

    public record HealthAssessment(
        decimal WellbeingScore,
        int PredictedMood,
        List<string> RiskFactors,
        List<string> Recommendations);

    // ---- Study -------------------------------------------------------

    public record StudySummaryRequest(string Text, int MaxSentences, string Language);

    public record StudySummary(string Summary, List<string> KeyPoints, string Source);

    public record QuizRequest(string Text, int QuestionCount, string Language);

    public record QuizQuestion(string Question, List<string> Options, int CorrectIndex, string Explanation);

    public record QuizResult(List<QuizQuestion> Questions, string Source);

    // ---- Career ------------------------------------------------------

    public record ResumeAnalysisRequest(
        string ResumeText,
        string? DesiredPosition,
        List<string> Skills,
        string Language);

    public record ResumeAnalysis(
        decimal OverallScore,
        List<string> Strengths,
        List<string> Weaknesses,
        List<string> MissingSkills,
        List<string> Suggestions,
        string Source);
}
