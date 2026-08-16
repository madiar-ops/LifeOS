using LifeOS.Application.DTO.Ai;
using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Dashboard;

/// <summary>
/// Главный экран целиком — один запрос вместо восьми.
///
/// Отдельный агрегирующий endpoint, а не сборка на фронтенде: восемь
/// параллельных запросов с мобильной сети — это восемь TLS-рукопожатий
/// и восемь проверок JWT ради одного экрана.
/// </summary>
public record DashboardResponse(
    DashboardPeriod Period,
    GoalsWidget Goals,
    TasksWidget Tasks,
    FinanceWidget Finance,
    HealthWidget Health,
    StudyWidget Study,
    CareerWidget Career,
    IReadOnlyList<RecommendationResponse> Recommendations,
    IReadOnlyList<RecentFileItem> RecentFiles,
    DateTime GeneratedAt);

public record DashboardPeriod(DateOnly From, DateOnly To, int Days);

// ---- Цели ----------------------------------------------------------------

public record GoalsWidget(
    int Total,
    int NotStarted,
    int InProgress,
    int Completed,
    int Cancelled,
    /// <summary>Доля завершённых целей, 0..1. Отменённые в знаменатель не входят.</summary>
    decimal CompletionRate,
    int OverdueCount,
    IReadOnlyList<GoalProgressItem> Upcoming);

public record GoalProgressItem(
    Guid Id,
    string Title,
    GoalStatus Status,
    PriorityLevel Priority,
    DateTime? Deadline,
    int TotalTasks,
    int CompletedTasks,
    /// <summary>Прогресс по задачам цели, 0..1. Цель без задач — 0.</summary>
    decimal Progress,
    bool IsOverdue);

// ---- Задачи --------------------------------------------------------------

public record TasksWidget(
    int Total,
    int Completed,
    int Pending,
    int OverdueCount,
    int DueTodayCount,
    int DueThisWeekCount,
    decimal CompletionRate,
    IReadOnlyList<TaskItemBrief> Urgent);

public record TaskItemBrief(
    Guid Id,
    string Title,
    DateTime? Deadline,
    string? GoalTitle,
    bool IsOverdue);

// ---- Финансы -------------------------------------------------------------

public record FinanceWidget(
    string Currency,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Balance,
    decimal SavingsRate,
    int TransactionCount,
    IReadOnlyList<CategoryShare> TopExpenseCategories,
    IReadOnlyList<MonthlyPoint> MonthlyTrend);

public record CategoryShare(string Category, decimal Amount, decimal Percentage);

public record MonthlyPoint(string Month, decimal Income, decimal Expense);

// ---- Здоровье ------------------------------------------------------------

public record HealthWidget(
    int EntriesCount,
    decimal? AverageSleepHours,
    int AverageSteps,
    int AverageWaterMl,
    decimal? LatestWeight,
    decimal? WeightChange,
    decimal AverageMood,
    /// <summary>Записи за период для графика, от старых к новым.</summary>
    IReadOnlyList<HealthPoint> Trend);

public record HealthPoint(DateOnly Date, decimal? SleepHours, int Steps, int WaterMl, int Mood);

// ---- Учёба и карьера -----------------------------------------------------

public record StudyWidget(
    int MaterialsCount,
    int SummarizedCount,
    int NotesCount,
    int QuizzesCount,
    int CompletedQuizzesCount,
    /// <summary>Средний результат пройденных тестов, 0..1. Null — тестов ещё не было.</summary>
    decimal? AverageQuizScore);

public record CareerWidget(
    bool HasResume,
    string? DesiredPosition,
    bool HasAiReview);

public record RecentFileItem(
    Guid Id,
    string FileName,
    string Url,
    ModuleType Module,
    long SizeBytes,
    DateTime CreatedAt);
