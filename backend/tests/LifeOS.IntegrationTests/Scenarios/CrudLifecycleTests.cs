using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Finance;
using LifeOS.Application.DTO.Goals;
using LifeOS.Application.DTO.Health;
using LifeOS.Application.DTO.Tasks;
using LifeOS.Domain.Enums;
using LifeOS.IntegrationTests.Infrastructure;

namespace LifeOS.IntegrationTests.Scenarios;

/// <summary>
/// Полные жизненные циклы сущностей через HTTP и настоящую PostgreSQL.
///
/// Отдельно проверяются правила, зафиксированные при проектировании БД
/// (Фаза 1): задача переживает удаление своей цели, запись здоровья уникальна
/// в пределах дня. Эти правила реализованы наполовину в EF-конфигурации и
/// наполовину в сервисе — unit-тест ни ту, ни другую половину не увидит.
/// </summary>
[Collection(ApiCollection.Name)]
public class CrudLifecycleTests
{
    private readonly ApiFixture _api;

    public CrudLifecycleTests(ApiFixture api) => _api = api;

    // ---- Цели и задачи ---------------------------------------------------

    [Fact]
    public async Task Цель_создаётся_изменяется_и_удаляется()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var created = await CreateGoalAsync(user, "Сдать сессию");
        created.Title.Should().Be("Сдать сессию");
        created.Status.Should().Be(GoalStatus.InProgress);

        var updated = await user.Client.PutAsJsonAsync(
            $"/api/goals/{created.Id}",
            new UpdateGoalRequest("Сдать сессию на отлично", "без троек",
                GoalStatus.Completed, PriorityLevel.High, null),
            ApiFixture.Json);

        var afterUpdate = await updated.Content.ReadFromJsonAsync<GoalResponse>(ApiFixture.Json);
        afterUpdate!.Title.Should().Be("Сдать сессию на отлично");
        afterUpdate.Status.Should().Be(GoalStatus.Completed);

        var deleted = await user.Client.DeleteAsync($"/api/goals/{created.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await user.Client.GetAsync($"/api/goals/{created.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Название_цели_обрезается_от_пробелов()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var goal = await CreateGoalAsync(user, "   Цель с пробелами   ");

        goal.Title.Should().Be("Цель с пробелами");
    }

    [Fact]
    public async Task Счётчики_задач_цели_обновляются_после_отметки_выполнения()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var goal = await CreateGoalAsync(user, "Цель со счётчиками");

        var first = await CreateTaskAsync(user, "Задача 1", goal.Id);
        await CreateTaskAsync(user, "Задача 2", goal.Id);

        var before = await user.Client.GetFromJsonAsync<GoalResponse>(
            $"/api/goals/{goal.Id}", ApiFixture.Json);
        before!.TotalTasks.Should().Be(2);
        before.CompletedTasks.Should().Be(0);

        var toggled = await user.Client.PatchAsync($"/api/tasks/{first.Id}/complete", null);
        var task = await toggled.Content.ReadFromJsonAsync<TaskResponse>(ApiFixture.Json);
        task!.Completed.Should().BeTrue();

        var after = await user.Client.GetFromJsonAsync<GoalResponse>(
            $"/api/goals/{goal.Id}", ApiFixture.Json);

        // Именно поэтому фронтенд после отметки задачи сбрасывает и кэш целей:
        // сервер пересчитывает счётчики, и старое значение выглядело бы багом.
        after!.CompletedTasks.Should().Be(1);
    }

    [Fact]
    public async Task Переключение_выполнения_работает_в_обе_стороны()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var task = await CreateTaskAsync(user, "Туда и обратно", goalId: null);

        var on = await user.Client.PatchAsync($"/api/tasks/{task.Id}/complete", null);
        var off = await user.Client.PatchAsync($"/api/tasks/{task.Id}/complete", null);

        (await on.Content.ReadFromJsonAsync<TaskResponse>(ApiFixture.Json))!.Completed.Should().BeTrue();
        (await off.Content.ReadFromJsonAsync<TaskResponse>(ApiFixture.Json))!.Completed.Should().BeFalse();
    }

    [Fact]
    public async Task Удаление_цели_сохраняет_её_задачи()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var goal = await CreateGoalAsync(user, "Временная цель");
        var task = await CreateTaskAsync(user, "Задача переживёт цель", goal.Id);

        await user.Client.DeleteAsync($"/api/goals/{goal.Id}");

        var survivor = await user.Client.GetFromJsonAsync<TaskResponse>(
            $"/api/tasks/{task.Id}", ApiFixture.Json);

        // Правило ON DELETE SET NULL из Фазы 1: задача остаётся, но теряет цель.
        // Об этом же предупреждает диалог удаления на фронтенде — тест проверяет,
        // что предупреждение не расходится с реальным поведением сервера.
        survivor.Should().NotBeNull();
        survivor!.GoalId.Should().BeNull();
        survivor.GoalTitle.Should().BeNull();
    }

    [Fact]
    public async Task Фильтр_по_статусу_и_поиск_по_названию_работают_вместе()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        await CreateGoalAsync(user, "Английский язык", GoalStatus.InProgress);
        await CreateGoalAsync(user, "Английская литература", GoalStatus.Completed);
        await CreateGoalAsync(user, "Математика", GoalStatus.InProgress);

        var page = await user.Client.GetFromJsonAsync<PagedResponse<GoalResponse>>(
            "/api/goals?status=InProgress&search=английск", ApiFixture.Json);

        // Поиск нечувствителен к регистру — так же, как на фронтенде.
        page!.Items.Should().ContainSingle().Which.Title.Should().Be("Английский язык");
    }

    // ---- Финансы ---------------------------------------------------------

    [Fact]
    public async Task Сводка_считает_доходы_расходы_и_доли_категорий()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var date = new DateOnly(2026, 2, 10);

        await CreateTransactionAsync(user, TransactionType.Income, "Стипендия", 100_000m, date);
        await CreateTransactionAsync(user, TransactionType.Expense, "Продукты", 30_000m, date);
        await CreateTransactionAsync(user, TransactionType.Expense, "Транспорт", 10_000m, date);

        var summary = await user.Client.GetFromJsonAsync<FinanceSummaryResponse>(
            "/api/finance/summary?from=2026-02-01&to=2026-02-28&currency=KZT", ApiFixture.Json);

        summary!.TotalIncome.Should().Be(100_000m);
        summary.TotalExpense.Should().Be(40_000m);
        summary.Balance.Should().Be(60_000m);
        summary.TransactionCount.Should().Be(3);

        var food = summary.ByCategory.Single(c => c.Category == "Продукты");

        // Доля считается от суммы своего типа: 30 000 — это 75% расходов,
        // а не 30% оборота. Иначе круговая диаграмма расходов врала бы.
        food.Percentage.Should().Be(75m);
    }

    [Fact]
    public async Task Операции_в_другой_валюте_не_попадают_в_сводку()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var date = new DateOnly(2026, 2, 10);

        await CreateTransactionAsync(user, TransactionType.Expense, "Подписки", 10m, date, "USD");
        await CreateTransactionAsync(user, TransactionType.Expense, "Продукты", 5_000m, date);

        var summary = await user.Client.GetFromJsonAsync<FinanceSummaryResponse>(
            "/api/finance/summary?from=2026-02-01&to=2026-02-28&currency=KZT", ApiFixture.Json);

        // Конвертации курсов в MVP нет, поэтому складывать доллары с тенге
        // означало бы показать пользователю бессмысленное число.
        summary!.TotalExpense.Should().Be(5_000m);
        summary.ByCategory.Should().NotContain(c => c.Category == "Подписки");
    }

    [Fact]
    public async Task Валюта_приводится_к_верхнему_регистру()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var created = await CreateTransactionAsync(
            user, TransactionType.Expense, "Кофе", 1_500m, new DateOnly(2026, 2, 10), "kzt");

        created.Currency.Should().Be("KZT", "иначе 'kzt' и 'KZT' стали бы разными валютами в сводке");
    }

    [Fact]
    public async Task Сводка_нового_пользователя_возвращает_нули_а_не_ошибку()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.GetAsync("/api/finance/summary");
        var summary = await response.Content.ReadFromJsonAsync<FinanceSummaryResponse>(ApiFixture.Json);

        // Деление на ноль при расчёте долей категорий — самая вероятная
        // ошибка на пустом аккаунте, и увидит её именно новый пользователь.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        summary!.TotalIncome.Should().Be(0);
        summary.TotalExpense.Should().Be(0);
        summary.ByCategory.Should().BeEmpty();
    }

    // ---- Здоровье --------------------------------------------------------

    [Fact]
    public async Task Вторая_запись_за_тот_же_день_отклоняется_с_кодом_health_duplicate_date()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var date = new DateOnly(2026, 2, 14);

        var first = await user.Client.PostAsJsonAsync(
            "/api/health/logs", HealthLog(date), ApiFixture.Json);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await user.Client.PostAsJsonAsync(
            "/api/health/logs", HealthLog(date), ApiFixture.Json);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemReader.ReadAsync(second)).Code.Should().Be("health.duplicate_date");
    }

    [Fact]
    public async Task Запись_здоровья_будущей_датой_отклоняется()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var response = await user.Client.PostAsJsonAsync(
            "/api/health/logs", HealthLog(tomorrow), ApiFixture.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemReader.ReadAsync(response)).Code.Should().Be("health.future_date");
    }

    [Fact]
    public async Task Одинаковая_дата_у_разных_пользователей_конфликтом_не_считается()
    {
        var first = await _api.CreateAuthenticatedUserAsync();
        var second = await _api.CreateAuthenticatedUserAsync();
        var date = new DateOnly(2026, 2, 15);

        var one = await first.Client.PostAsJsonAsync("/api/health/logs", HealthLog(date), ApiFixture.Json);
        var two = await second.Client.PostAsJsonAsync("/api/health/logs", HealthLog(date), ApiFixture.Json);

        // Уникальность составная — (UserId, Date), а не одна только дата.
        one.IsSuccessStatusCode.Should().BeTrue();
        two.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Незаполненные_измерения_сохраняются_как_null()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.PostAsJsonAsync(
            "/api/health/logs",
            new CreateHealthLogRequest(new DateOnly(2026, 2, 16), null, null, MoodLevel.Neutral, 0, 0),
            ApiFixture.Json);

        var log = await response.Content.ReadFromJsonAsync<HealthLogResponse>(ApiFixture.Json);

        // «Не взвешивался» обязано отличаться от «весил 0 кг»: ноль попал бы
        // в датасет health-модели как настоящее измерение и исказил обучение.
        log!.Weight.Should().BeNull();
        log.SleepHours.Should().BeNull();
    }

    // ---- Вспомогательные методы -----------------------------------------

    private static CreateHealthLogRequest HealthLog(DateOnly date)
        => new(date, 72m, 7.5m, MoodLevel.Good, 2_000, 8_000);

    private async Task<GoalResponse> CreateGoalAsync(
        TestUser user, string title, GoalStatus status = GoalStatus.InProgress)
    {
        var response = await user.Client.PostAsJsonAsync(
            "/api/goals",
            new CreateGoalRequest(title, null, status, PriorityLevel.Medium, null),
            ApiFixture.Json);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalResponse>(ApiFixture.Json))!;
    }

    private async Task<TaskResponse> CreateTaskAsync(TestUser user, string title, Guid? goalId)
    {
        var response = await user.Client.PostAsJsonAsync(
            "/api/tasks", new CreateTaskRequest(title, goalId, null), ApiFixture.Json);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse>(ApiFixture.Json))!;
    }

    private async Task<TransactionResponse> CreateTransactionAsync(
        TestUser user,
        TransactionType type,
        string category,
        decimal amount,
        DateOnly date,
        string currency = "KZT")
    {
        var response = await user.Client.PostAsJsonAsync(
            "/api/finance/transactions",
            new CreateTransactionRequest(type, category, amount, currency, date, null),
            ApiFixture.Json);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TransactionResponse>(ApiFixture.Json))!;
    }
}
