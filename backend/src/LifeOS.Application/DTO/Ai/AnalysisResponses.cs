namespace LifeOS.Application.DTO.Ai;

public record FinanceForecastResponse(
    decimal PredictedExpense,
    decimal PredictedBalance,
    string Trend,
    string? TopCategory,
    decimal SavingsRate,
    string Currency,
    int MonthsAnalyzed);

public record HealthAssessmentResponse(
    decimal WellbeingScore,
    int PredictedMood,
    IReadOnlyList<string> RiskFactors,
    IReadOnlyList<string> Recommendations,
    int DaysAnalyzed);
