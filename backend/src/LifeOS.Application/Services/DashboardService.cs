using AutoMapper;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Dashboard;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Application.Services;

/// <summary>
/// Агрегация главного экрана.
///
/// Принципы, которых держится этот сервис:
///
/// 1. Считает БД, а не приложение. Ни один виджет не выгружает коллекции
///    целиком ради подсчёта — используются GroupBy и агрегатные функции.
///
/// 2. Запросы идут последовательно. EF Core не поддерживает несколько
///    одновременных операций на одном DbContext: Task.WhenAll здесь дал бы
///    InvalidOperationException, а не ускорение.
///
/// 3. Никаких вызовов AI. Dashboard обязан открываться мгновенно, а поход
///    в FastAPI занимает секунды. Рекомендации берутся из таблицы —
///    те, что уже посчитаны при явных запросах анализа.
/// </summary>
public class DashboardService : IDashboardService
{
    private const string DefaultCurrency = "KZT";
    private const int MaxUpcomingGoals = 5;
    private const int MaxUrgentTasks = 5;
    private const int MaxRecommendations = 5;
    private const int MaxRecentFiles = 5;
    private const int MaxTopCategories = 5;
    private const int TrendMonths = 6;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IMapper _mapper;

    public DashboardService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _mapper = mapper;
    }

    public async Task<DashboardResponse> GetAsync(int days, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var to = _dateTime.Today;
        var from = to.AddDays(-Math.Clamp(days, 1, 365) + 1);
        var period = new DashboardPeriod(from, to, to.DayNumber - from.DayNumber + 1);

        var goals = await BuildGoalsAsync(userId, cancellationToken);
        var tasks = await BuildTasksAsync(userId, cancellationToken);
        var finance = await BuildFinanceAsync(userId, from, to, cancellationToken);
        var health = await BuildHealthAsync(userId, from, to, cancellationToken);
        var study = await BuildStudyAsync(userId, cancellationToken);
        var career = await BuildCareerAsync(userId, cancellationToken);
        var recommendations = await BuildRecommendationsAsync(userId, cancellationToken);
        var files = await BuildRecentFilesAsync(userId, cancellationToken);

        return new DashboardResponse(
            period, goals, tasks, finance, health, study, career,
            recommendations, files, _dateTime.UtcNow);
    }

    // ---- Цели ------------------------------------------------------------

    private async Task<GoalsWidget> BuildGoalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;

        // Один GROUP BY вместо четырёх COUNT-запросов.
        var byStatus = await _unitOfWork.Goals.Query()
            .Where(g => g.UserId == userId)
            .GroupBy(g => g.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountOf(GoalStatus status) =>
            byStatus.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var total = byStatus.Sum(x => x.Count);
        var completed = CountOf(GoalStatus.Completed);
        var cancelled = CountOf(GoalStatus.Cancelled);

        // Отменённые цели исключаем из знаменателя: они не провал,
        // а осознанный отказ, и портить ими статистику неправильно.
        var relevant = total - cancelled;

        var overdue = await _unitOfWork.Goals.CountAsync(
            g => g.UserId == userId
                 && g.Deadline != null
                 && g.Deadline < now
                 && g.Status != GoalStatus.Completed
                 && g.Status != GoalStatus.Cancelled,
            cancellationToken);

        // Ближайшие активные цели: сначала с дедлайном, затем по приоритету.
        var upcoming = await _unitOfWork.Goals.Query()
            .Where(g => g.UserId == userId
                        && g.Status != GoalStatus.Completed
                        && g.Status != GoalStatus.Cancelled)
            .OrderBy(g => g.Deadline == null)
            .ThenBy(g => g.Deadline)
            .ThenByDescending(g => g.Priority)
            .Take(MaxUpcomingGoals)
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.Status,
                g.Priority,
                g.Deadline,
                TotalTasks = g.Tasks.Count,
                CompletedTasks = g.Tasks.Count(t => t.Completed)
            })
            .ToListAsync(cancellationToken);

        return new GoalsWidget(
            total,
            CountOf(GoalStatus.NotStarted),
            CountOf(GoalStatus.InProgress),
            completed,
            cancelled,
            relevant == 0 ? 0m : Math.Round((decimal)completed / relevant, 4),
            overdue,
            upcoming.Select(g => new GoalProgressItem(
                g.Id, g.Title, g.Status, g.Priority, g.Deadline,
                g.TotalTasks, g.CompletedTasks,
                g.TotalTasks == 0 ? 0m : Math.Round((decimal)g.CompletedTasks / g.TotalTasks, 4),
                g.Deadline != null && g.Deadline < now)).ToList());
    }

    // ---- Задачи ----------------------------------------------------------

    private async Task<TasksWidget> BuildTasksAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;
        var endOfToday = _dateTime.Today.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();
        var endOfWeek = _dateTime.Today.AddDays(7).ToDateTime(TimeOnly.MaxValue).ToUniversalTime();

        var counts = await _unitOfWork.Tasks.Query()
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Completed)
            .Select(g => new { Completed = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var completed = counts.FirstOrDefault(x => x.Completed)?.Count ?? 0;
        var pending = counts.FirstOrDefault(x => !x.Completed)?.Count ?? 0;
        var total = completed + pending;

        var pendingWithDeadline = _unitOfWork.Tasks.Query()
            .Where(t => t.UserId == userId && !t.Completed && t.Deadline != null);

        var overdue = await pendingWithDeadline.CountAsync(t => t.Deadline < now, cancellationToken);

        var dueToday = await pendingWithDeadline.CountAsync(
            t => t.Deadline >= now && t.Deadline <= endOfToday, cancellationToken);

        var dueThisWeek = await pendingWithDeadline.CountAsync(
            t => t.Deadline >= now && t.Deadline <= endOfWeek, cancellationToken);

        var urgent = await _unitOfWork.Tasks.Query()
            .Where(t => t.UserId == userId && !t.Completed)
            .OrderBy(t => t.Deadline == null)
            .ThenBy(t => t.Deadline)
            .Take(MaxUrgentTasks)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Deadline,
                GoalTitle = t.Goal != null ? t.Goal.Title : null
            })
            .ToListAsync(cancellationToken);

        return new TasksWidget(
            total, completed, pending, overdue, dueToday, dueThisWeek,
            total == 0 ? 0m : Math.Round((decimal)completed / total, 4),
            urgent.Select(t => new TaskItemBrief(
                t.Id, t.Title, t.Deadline, t.GoalTitle,
                t.Deadline != null && t.Deadline < now)).ToList());
    }

    // ---- Финансы ---------------------------------------------------------

    private async Task<FinanceWidget> BuildFinanceAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // Валюта не смешивается: конвертации курсов в MVP нет,
        // поэтому берём ту, в которой пользователь ведёт учёт чаще всего.
        var currency = await _unitOfWork.Transactions.Query()
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(cancellationToken) ?? DefaultCurrency;

        var periodQuery = _unitOfWork.Transactions.Query()
            .Where(t => t.UserId == userId
                        && t.Currency == currency
                        && t.Date >= from && t.Date <= to);

        var totals = await periodQuery
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Amount = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var income = totals.FirstOrDefault(x => x.Type == TransactionType.Income)?.Amount ?? 0m;
        var expense = totals.FirstOrDefault(x => x.Type == TransactionType.Expense)?.Amount ?? 0m;

        var topCategories = await periodQuery
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Amount = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Amount)
            .Take(MaxTopCategories)
            .ToListAsync(cancellationToken);

        // Тренд берётся за 6 месяцев независимо от периода виджета:
        // график из двух точек бессмыслен.
        var trendFrom = new DateOnly(to.Year, to.Month, 1).AddMonths(-(TrendMonths - 1));

        var trend = await _unitOfWork.Transactions.Query()
            .Where(t => t.UserId == userId && t.Currency == currency && t.Date >= trendFrom)
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => (decimal?)t.Amount) ?? 0m,
                Expense = g.Where(t => t.Type == TransactionType.Expense).Sum(t => (decimal?)t.Amount) ?? 0m
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        return new FinanceWidget(
            currency,
            income,
            expense,
            income - expense,
            income == 0 ? 0m : Math.Round(Math.Max(0m, (income - expense) / income), 4),
            totals.Sum(x => x.Count),
            topCategories.Select(c => new CategoryShare(
                c.Category, c.Amount,
                expense == 0 ? 0m : Math.Round(c.Amount / expense * 100, 2))).ToList(),
            trend.Select(t => new MonthlyPoint(
                $"{t.Year:D4}-{t.Month:D2}", t.Income, t.Expense)).ToList());
    }

    // ---- Здоровье --------------------------------------------------------

    private async Task<HealthWidget> BuildHealthAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var logs = await _unitOfWork.HealthLogs.Query()
            .Where(h => h.UserId == userId && h.Date >= from && h.Date <= to)
            .OrderBy(h => h.Date)
            .Select(h => new
            {
                h.Date,
                h.SleepHours,
                h.Steps,
                h.WaterMl,
                h.Weight,
                h.Mood
            })
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
            return new HealthWidget(0, null, 0, 0, null, null, 0m, Array.Empty<HealthPoint>());

        var withWeight = logs.Where(l => l.Weight.HasValue).ToList();

        // Изменение веса считаем между первым и последним измерением —
        // промежуточные пропуски не должны ломать расчёт.
        decimal? weightChange = withWeight.Count >= 2
            ? withWeight[^1].Weight!.Value - withWeight[0].Weight!.Value
            : null;

        var sleepValues = logs.Where(l => l.SleepHours.HasValue).Select(l => l.SleepHours!.Value).ToList();

        return new HealthWidget(
            logs.Count,
            sleepValues.Count > 0 ? Math.Round(sleepValues.Average(), 2) : null,
            (int)Math.Round(logs.Average(l => (double)l.Steps)),
            (int)Math.Round(logs.Average(l => (double)l.WaterMl)),
            withWeight.Count > 0 ? withWeight[^1].Weight : null,
            weightChange.HasValue ? Math.Round(weightChange.Value, 2) : null,
            Math.Round((decimal)logs.Average(l => (double)(int)l.Mood), 2),
            logs.Select(l => new HealthPoint(
                l.Date, l.SleepHours, l.Steps, l.WaterMl, (int)l.Mood)).ToList());
    }

    // ---- Учёба и карьера -------------------------------------------------

    private async Task<StudyWidget> BuildStudyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var materials = await _unitOfWork.StudyMaterials.Query()
            .Where(m => m.UserId == userId)
            .Select(m => new { HasSummary = m.Summary != null })
            .ToListAsync(cancellationToken);

        var notesCount = await _unitOfWork.StudyNotes.CountAsync(
            n => n.UserId == userId, cancellationToken);

        var quizzes = await _unitOfWork.Quizzes.Query()
            .Where(q => q.UserId == userId)
            .Select(q => new { q.Score, q.TotalQuestions })
            .ToListAsync(cancellationToken);

        var graded = quizzes.Where(q => q.Score.HasValue && q.TotalQuestions > 0).ToList();

        return new StudyWidget(
            materials.Count,
            materials.Count(m => m.HasSummary),
            notesCount,
            quizzes.Count,
            graded.Count,
            graded.Count == 0
                ? null
                : Math.Round(graded.Average(q => (decimal)q.Score!.Value / q.TotalQuestions), 4));
    }

    private async Task<CareerWidget> BuildCareerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _unitOfWork.CareerProfiles.Query()
            .Where(c => c.UserId == userId)
            .Select(c => new
            {
                HasResume = c.ResumeFileId != null,
                c.DesiredPosition,
                HasReview = c.AiReview != null
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Профиль создаётся лениво, поэтому его может ещё не быть —
        // это нормальное состояние, а не ошибка.
        return profile is null
            ? new CareerWidget(false, null, false)
            : new CareerWidget(profile.HasResume, profile.DesiredPosition, profile.HasReview);
    }

    // ---- Рекомендации и файлы --------------------------------------------

    private async Task<IReadOnlyList<RecommendationResponse>> BuildRecommendationsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var items = await _unitOfWork.Recommendations.Query()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Confidence)
            .Take(MaxRecommendations)
            .ToListAsync(cancellationToken);

        return items.Select(_mapper.Map<RecommendationResponse>).ToList();
    }

    private async Task<IReadOnlyList<RecentFileItem>> BuildRecentFilesAsync(
        Guid userId, CancellationToken cancellationToken)
        => await _unitOfWork.Files.Query()
            .Where(f => f.UserId == userId && f.Module != ModuleType.Avatar)
            .OrderByDescending(f => f.CreatedAt)
            .Take(MaxRecentFiles)
            .Select(f => new RecentFileItem(
                f.Id, f.FileName, f.FirebaseUrl, f.Module, f.SizeBytes, f.CreatedAt))
            .ToListAsync(cancellationToken);
}
