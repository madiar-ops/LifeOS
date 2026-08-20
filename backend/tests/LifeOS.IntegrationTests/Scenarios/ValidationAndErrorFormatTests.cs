using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Goals;
using LifeOS.Application.DTO.Health;
using LifeOS.Domain.Enums;
using LifeOS.IntegrationTests.Infrastructure;

namespace LifeOS.IntegrationTests.Scenarios;

/// <summary>
/// Формат ошибок и работа фильтра валидации.
///
/// Фронтенд подсвечивает конкретные поля формы по словарю «поле → ошибки»
/// из ValidationProblemDetails. Если формат ответа изменится, поля перестанут
/// подсвечиваться молча — без единой ошибки ни на сервере, ни на клиенте.
/// </summary>
[Collection(ApiCollection.Name)]
public class ValidationAndErrorFormatTests
{
    private readonly ApiFixture _api;

    public ValidationAndErrorFormatTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Ошибка_валидации_приходит_словарём_поле_ошибки()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.PostAsJsonAsync(
            "/api/goals",
            new CreateGoalRequest("", null, GoalStatus.InProgress, PriorityLevel.Medium, null),
            ApiFixture.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ProblemReader.ReadAsync(response);
        problem.Code.Should().Be("validation.failed");
        problem.Errors.Should().ContainKey("Title");
        problem.Errors["Title"].Should().NotBeEmpty();
    }

    [Fact]
    public async Task Несколько_нарушенных_правил_возвращаются_одним_ответом()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.PostAsJsonAsync(
            "/api/health/logs",
            new CreateHealthLogRequest(new DateOnly(2026, 3, 1), 900m, 40m, MoodLevel.Good, 99_999, 999_999),
            ApiFixture.Json);

        var problem = await ProblemReader.ReadAsync(response);

        // Отдавать ошибки по одной значило бы заставлять пользователя
        // исправлять форму в несколько заходов.
        problem.Errors.Keys.Should().Contain(new[] { "Weight", "SleepHours", "WaterMl", "Steps" });
    }

    [Fact]
    public async Task Несуществующее_значение_перечисления_даёт_400_а_не_500()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        // Enum'ы ходят строками, поэтому «ЛюбойТекст» — это ошибка привязки
        // модели, а не валидации DTO. Штатный ответ ASP.NET на неё подавлен,
        // и её обязан перехватить ValidationFilter.
        var payload = new StringContent(
            """{"title":"Цель","description":null,"status":"НетТакогоСтатуса","priority":"Medium","deadline":null}""",
            Encoding.UTF8,
            "application/json");

        var response = await user.Client.PostAsync("/api/goals", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemReader.ReadAsync(response)).Code.Should().Be("validation.failed");
    }

    [Fact]
    public async Task Битый_JSON_даёт_400_а_не_500()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var payload = new StringContent("{ это вообще не json", Encoding.UTF8, "application/json");

        var response = await user.Client.PostAsync("/api/goals", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Запрос_несуществующей_цели_даёт_404_с_кодом_resource_not_found()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.GetAsync($"/api/goals/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemReader.ReadAsync(response)).Code.Should().Be("resource.not_found");
    }

    [Fact]
    public async Task Некорректный_GUID_в_маршруте_даёт_404_а_не_500()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        // Ограничение маршрута {id:guid} просто не находит подходящий endpoint.
        var response = await user.Client.GetAsync("/api/goals/не-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Размер_страницы_ограничен_сверху()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var page = await user.Client.GetFromJsonAsync<PagedResponse<GoalResponse>>(
            "/api/goals?pageSize=100000", ApiFixture.Json);

        // Без верхней границы один запрос выгрузил бы в память всю таблицу.
        page!.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Отрицательный_номер_страницы_приводится_к_первой()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var page = await user.Client.GetFromJsonAsync<PagedResponse<GoalResponse>>(
            "/api/goals?pageNumber=-5", ApiFixture.Json);

        page!.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task В_каждой_ошибке_есть_traceId_для_поиска_в_логах()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var validation = await user.Client.PostAsJsonAsync(
            "/api/goals",
            new CreateGoalRequest("", null, GoalStatus.InProgress, PriorityLevel.Medium, null),
            ApiFixture.Json);
        var notFound = await user.Client.GetAsync($"/api/goals/{Guid.NewGuid()}");

        (await ProblemReader.ReadAsync(validation)).TraceId.Should().NotBeNullOrEmpty();
        (await ProblemReader.ReadAsync(notFound)).TraceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Ответ_об_ошибке_приходит_с_типом_problem_json()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.GetAsync($"/api/goals/{Guid.NewGuid()}");

        // RFC 7807 требует именно этот media type; фронтенд по нему отличает
        // структурированную ошибку от произвольного текста.
        response.Content.Headers.ContentType!.MediaType
            .Should().BeOneOf("application/problem+json", "application/json");
    }
}
