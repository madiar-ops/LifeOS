using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Goals;
using LifeOS.Application.DTO.Tasks;
using LifeOS.Domain.Enums;
using LifeOS.IntegrationTests.Infrastructure;

namespace LifeOS.IntegrationTests.Scenarios;

/// <summary>
/// Проверки изоляции данных между пользователями (защита от IDOR).
///
/// Это самая важная группа тестов проекта. JWT отвечает только на вопрос
/// «кто ты»; на вопрос «твоё ли это» отвечает фильтр по UserId в каждом
/// запросе и <c>CrudGuard</c>. Пропущенная проверка в одном методе открывает
/// чужие данные, и обнаружить это по внешнему виду приложения невозможно.
/// </summary>
[Collection(ApiCollection.Name)]
public class OwnershipTests
{
    private readonly ApiFixture _api;

    public OwnershipTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Чужая_цель_недоступна_ни_на_чтение_ни_на_изменение_ни_на_удаление()
    {
        var owner = await _api.CreateAuthenticatedUserAsync();
        var stranger = await _api.CreateAuthenticatedUserAsync();

        var goal = await CreateGoalAsync(owner, "Личная цель владельца");

        var read = await stranger.Client.GetAsync($"/api/goals/{goal.Id}");
        var update = await stranger.Client.PutAsJsonAsync(
            $"/api/goals/{goal.Id}",
            new UpdateGoalRequest("Взломано", null, GoalStatus.Cancelled, PriorityLevel.Low, null),
            ApiFixture.Json);
        var delete = await stranger.Client.DeleteAsync($"/api/goals/{goal.Id}");

        // Именно 404, а не 403: 403 подтвердил бы существование объекта
        // и позволил бы перебором выяснить, какие идентификаторы заняты.
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // И, разумеется, цель осталась нетронутой.
        var afterAttack = await owner.Client.GetFromJsonAsync<GoalResponse>(
            $"/api/goals/{goal.Id}", ApiFixture.Json);
        afterAttack!.Title.Should().Be("Личная цель владельца");
    }

    [Fact]
    public async Task Существующая_и_несуществующая_чужая_цель_неотличимы_по_ответу()
    {
        var owner = await _api.CreateAuthenticatedUserAsync();
        var stranger = await _api.CreateAuthenticatedUserAsync();
        var goal = await CreateGoalAsync(owner, "Цель владельца");

        var foreign = await stranger.Client.GetAsync($"/api/goals/{goal.Id}");
        var missing = await stranger.Client.GetAsync($"/api/goals/{Guid.NewGuid()}");

        foreign.StatusCode.Should().Be(missing.StatusCode);

        var foreignProblem = await ProblemReader.ReadAsync(foreign);
        var missingProblem = await ProblemReader.ReadAsync(missing);
        foreignProblem.Code.Should().Be(missingProblem.Code).And.Be("resource.not_found");
    }

    [Fact]
    public async Task Список_целей_содержит_только_свои_записи()
    {
        var first = await _api.CreateAuthenticatedUserAsync();
        var second = await _api.CreateAuthenticatedUserAsync();

        await CreateGoalAsync(first, "Цель первого пользователя");
        await CreateGoalAsync(second, "Цель второго пользователя");

        var page = await second.Client.GetFromJsonAsync<PagedResponse<GoalResponse>>(
            "/api/goals?pageSize=100", ApiFixture.Json);

        page!.Items.Should().OnlyContain(goal => goal.Title == "Цель второго пользователя");
    }

    [Fact]
    public async Task Задачу_нельзя_привязать_к_чужой_цели()
    {
        var owner = await _api.CreateAuthenticatedUserAsync();
        var stranger = await _api.CreateAuthenticatedUserAsync();
        var goal = await CreateGoalAsync(owner, "Цель владельца");

        var response = await stranger.Client.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskRequest("Задача-подкидыш", goal.Id, null),
            ApiFixture.Json);

        // Иначе через поле goalId можно было бы засорять чужие цели
        // своими задачами и искажать их счётчики выполнения.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Чужую_задачу_нельзя_отметить_выполненной()
    {
        var owner = await _api.CreateAuthenticatedUserAsync();
        var stranger = await _api.CreateAuthenticatedUserAsync();

        var created = await owner.Client.PostAsJsonAsync(
            "/api/tasks", new CreateTaskRequest("Задача владельца", null, null), ApiFixture.Json);
        var task = await created.Content.ReadFromJsonAsync<TaskResponse>(ApiFixture.Json);

        var response = await stranger.Client.PatchAsync($"/api/tasks/{task!.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Чужой_профиль_по_идентификатору_недоступен()
    {
        var owner = await _api.CreateAuthenticatedUserAsync();
        var stranger = await _api.CreateAuthenticatedUserAsync();

        var response = await stranger.Client.GetAsync($"/api/users/{owner.Id}");

        // Здесь ответ — 403, а не 404: сам факт существования пользователя
        // и так подтверждается формой регистрации («email уже занят»),
        // скрывать его бессмысленно, а честный код читается яснее.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProblemReader.ReadAsync(response)).Code.Should().Be("access.forbidden");
    }

    [Fact]
    public async Task Свой_профиль_по_идентификатору_доступен()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.GetAsync($"/api/users/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<GoalResponse> CreateGoalAsync(TestUser user, string title)
    {
        var response = await user.Client.PostAsJsonAsync(
            "/api/goals",
            new CreateGoalRequest(title, null, GoalStatus.InProgress, PriorityLevel.Medium, null),
            ApiFixture.Json);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<GoalResponse>(ApiFixture.Json))!;
    }
}
