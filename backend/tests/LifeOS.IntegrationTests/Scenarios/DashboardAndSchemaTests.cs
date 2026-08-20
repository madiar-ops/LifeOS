using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LifeOS.Application.DTO.Dashboard;
using LifeOS.Application.DTO.Goals;
using LifeOS.Application.DTO.Tasks;
using LifeOS.Domain.Enums;
using LifeOS.Infrastructure.Data;
using LifeOS.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LifeOS.IntegrationTests.Scenarios;

/// <summary>
/// Главный экран.
///
/// Dashboard — единственное место, где все агрегаты считаются одним походом
/// в БД через GroupBy на стороне PostgreSQL. Именно такие запросы EF может
/// не суметь перевести в SQL, и провайдер InMemory этого не покажет:
/// он выполняет LINQ в памяти и «переварит» что угодно.
/// </summary>
[Collection(ApiCollection.Name)]
public class DashboardTests
{
    private readonly ApiFixture _api;

    public DashboardTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Новый_пользователь_видит_нули_а_не_ошибку()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.GetAsync("/api/dashboard");

        // Пустой аккаунт — первый экран, который видит каждый новый
        // пользователь. Деление на ноль в расчёте процентов выполнения
        // проявилось бы именно здесь и именно у него.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(ApiFixture.Json);
        dashboard!.Goals.Total.Should().Be(0);
        dashboard.Goals.CompletionRate.Should().Be(0);
        dashboard.Tasks.Total.Should().Be(0);
        dashboard.Tasks.CompletionRate.Should().Be(0);
        dashboard.Finance.TransactionCount.Should().Be(0);
        dashboard.Health.EntriesCount.Should().Be(0);
        dashboard.Study.MaterialsCount.Should().Be(0);
        dashboard.Career.HasResume.Should().BeFalse();
        dashboard.Recommendations.Should().BeEmpty();
        dashboard.RecentFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Сводка_отражает_созданные_цели_и_задачи()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var goal = await user.Client.PostAsJsonAsync(
            "/api/goals",
            new CreateGoalRequest("Цель для сводки", null, GoalStatus.Completed, PriorityLevel.High, null),
            ApiFixture.Json);
        var created = await goal.Content.ReadFromJsonAsync<GoalResponse>(ApiFixture.Json);

        var task = await user.Client.PostAsJsonAsync(
            "/api/tasks", new CreateTaskRequest("Задача для сводки", created!.Id, null), ApiFixture.Json);
        var createdTask = await task.Content.ReadFromJsonAsync<TaskResponse>(ApiFixture.Json);

        await user.Client.PatchAsync($"/api/tasks/{createdTask!.Id}/complete", null);

        var dashboard = await user.Client.GetFromJsonAsync<DashboardResponse>(
            "/api/dashboard", ApiFixture.Json);

        dashboard!.Goals.Total.Should().Be(1);
        dashboard.Goals.Completed.Should().Be(1);
        dashboard.Goals.CompletionRate.Should().Be(100);
        dashboard.Tasks.Total.Should().Be(1);
        dashboard.Tasks.Completed.Should().Be(1);
        dashboard.Tasks.Pending.Should().Be(0);
    }

    [Fact]
    public async Task Сводка_не_видит_данные_других_пользователей()
    {
        var owner = await _api.CreateAuthenticatedUserAsync();
        var stranger = await _api.CreateAuthenticatedUserAsync();

        await owner.Client.PostAsJsonAsync(
            "/api/goals",
            new CreateGoalRequest("Чужая цель", null, GoalStatus.InProgress, PriorityLevel.Low, null),
            ApiFixture.Json);

        var dashboard = await stranger.Client.GetFromJsonAsync<DashboardResponse>(
            "/api/dashboard", ApiFixture.Json);

        dashboard!.Goals.Total.Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(365)]
    public async Task Любая_допустимая_глубина_периода_обрабатывается(int days)
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.GetAsync($"/api/dashboard?days={days}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(ApiFixture.Json);
        dashboard!.Period.Days.Should().Be(days);
        dashboard.Period.From.Should().BeOnOrBefore(dashboard.Period.To);
    }

    [Fact]
    public async Task Момент_формирования_сводки_приходит_в_UTC()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var dashboard = await user.Client.GetFromJsonAsync<DashboardResponse>(
            "/api/dashboard", ApiFixture.Json);

        // Вся система работает во времени UTC (UtcDateTimeConverter в EF),
        // и фронтенд переводит в местное время сам. Локальная зона сервера
        // здесь просочиться не должна.
        dashboard!.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }
}

/// <summary>
/// Соответствие модели EF Core и реальной схемы БД.
///
/// Расхождение возникает бесшумно: свойство добавили в сущность, миграцию
/// создать забыли. Приложение стартует, Swagger открывается, и падает лишь
/// первый запрос к затронутой таблице — в проде.
/// </summary>
[Collection(ApiCollection.Name)]
public class SchemaIntegrityTests
{
    private readonly ApiFixture _api;

    public SchemaIntegrityTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Непринятых_миграций_не_осталось()
    {
        using var scope = _api.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await context.Database.GetPendingMigrationsAsync();

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task Каждая_таблица_читается_настоящим_SQL_запросом()
    {
        using var scope = _api.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Запрос к каждой таблице перечисляет ВСЕ колонки модели. Если хоть
        // одного столбца нет в схеме, Npgsql бросит 42703 (undefined_column) —
        // это самый простой и надёжный способ поймать забытую миграцию,
        // не залезая во внутренние API EF Core.
        var probes = new List<Func<Task>>
        {
            () => context.Users.Take(1).ToListAsync(),
            () => context.RefreshTokens.Take(1).ToListAsync(),
            () => context.Goals.Take(1).ToListAsync(),
            () => context.Tasks.Take(1).ToListAsync(),
            () => context.Transactions.Take(1).ToListAsync(),
            () => context.HealthLogs.Take(1).ToListAsync(),
            () => context.StudyMaterials.Take(1).ToListAsync(),
            () => context.StudyNotes.Take(1).ToListAsync(),
            () => context.Quizzes.Take(1).ToListAsync(),
            () => context.CareerProfiles.Take(1).ToListAsync(),
            () => context.Recommendations.Take(1).ToListAsync(),
            () => context.AiHistory.Take(1).ToListAsync(),
            () => context.Files.Take(1).ToListAsync()
        };

        foreach (var probe in probes)
            await probe.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Проверка_здоровья_подтверждает_связь_с_базой()
    {
        var response = await _api.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
