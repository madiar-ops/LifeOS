using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LifeOS.Application.DTO.Auth;
using LifeOS.IntegrationTests.Infrastructure;

namespace LifeOS.IntegrationTests.Scenarios;

/// <summary>
/// Сквозные проверки аутентификации — через настоящий HTTP, настоящий JWT
/// и настоящую PostgreSQL.
///
/// Unit-тесты <c>AuthService</c> проверяют логику; здесь проверяется то,
/// что unit-тестам недоступно: реальная выдача и проверка подписи токена,
/// коды ответов, формат ошибок и работа уникального индекса по email.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthFlowTests
{
    private readonly ApiFixture _api;

    public AuthFlowTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Регистрация_выдаёт_рабочий_токен()
    {
        var user = await _api.CreateAuthenticatedUserAsync();

        var response = await user.Client.GetAsync("/api/auth/me");
        var profile = await response.Content.ReadFromJsonAsync<UserResponse>(ApiFixture.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        profile!.Id.Should().Be(user.Id);
        profile.Email.Should().Be(user.Email.ToLowerInvariant());
    }

    [Fact]
    public async Task Повторная_регистрация_того_же_email_даёт_409_с_машиночитаемым_кодом()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("Другой", "Человек", user.Email, "Passw0rd!"),
            ApiFixture.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await ProblemReader.ReadAsync(response);
        problem.Code.Should().Be("user.email_taken");
        problem.TraceId.Should().NotBeNullOrEmpty("по traceId ошибку находят в логах Serilog");
    }

    [Fact]
    public async Task Email_не_различает_регистр_при_повторной_регистрации()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("Другой", "Человек", user.Email.ToUpperInvariant(), "Passw0rd!"),
            ApiFixture.Json);

        // Нормализация email в AuthService и уникальный индекс в БД должны
        // давать один и тот же результат. Расхождение позволило бы завести
        // два аккаунта на один почтовый ящик.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Вход_с_неверным_паролем_отклоняется_кодом_auth_invalid_credentials()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(user.Email, "НеверныйПароль1"), ApiFixture.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemReader.ReadAsync(response)).Code.Should().Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task Вход_с_правильным_паролем_выдаёт_новую_пару_токенов()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(user.Email, user.Password), ApiFixture.Json);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ApiFixture.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        auth!.RefreshToken.Should().NotBe(user.Auth.RefreshToken, "каждый вход выдаёт собственный токен");
        auth.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Обновление_токена_ротирует_refresh_и_гасит_прежний()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();

        var refreshed = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(user.Auth.RefreshToken), ApiFixture.Json);

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);

        var pair = await refreshed.Content.ReadFromJsonAsync<AuthResponse>(ApiFixture.Json);
        pair!.RefreshToken.Should().NotBe(user.Auth.RefreshToken);

        // Новый access-токен обязан работать сразу.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Повторное_использование_погашенного_токена_гасит_всю_цепочку()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();
        var stolen = user.Auth.RefreshToken;

        // Легитимный клиент обновился — исходный токен погашен и заменён.
        var first = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(stolen), ApiFixture.Json);
        var issued = (await first.Content.ReadFromJsonAsync<AuthResponse>(ApiFixture.Json))!.RefreshToken;

        // «Злоумышленник» предъявляет украденную копию того же токена.
        var reuse = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(stolen), ApiFixture.Json);

        reuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemReader.ReadAsync(reuse)).Code.Should().Be("auth.token_reuse_detected");

        // Ключевая часть защиты: гасится не только предъявленный токен,
        // но и выданный законному клиенту — цепочка обрывается целиком.
        var afterAlarm = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(issued), ApiFixture.Json);

        afterAlarm.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Выход_делает_refresh_токен_непригодным()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();

        var logout = await client.PostAsJsonAsync(
            "/api/auth/logout", new RefreshRequest(user.Auth.RefreshToken), ApiFixture.Json);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterLogout = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(user.Auth.RefreshToken), ApiFixture.Json);

        // Погашенный выходом токен предъявляется как отозванный, поэтому
        // ответ — тот же сигнал компрометации. Пользоваться им нельзя.
        afterLogout.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Повторный_выход_остаётся_успешным()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();
        var request = new RefreshRequest(user.Auth.RefreshToken);

        await client.PostAsJsonAsync("/api/auth/logout", request, ApiFixture.Json);
        var second = await client.PostAsJsonAsync("/api/auth/logout", request, ApiFixture.Json);

        second.StatusCode.Should().Be(HttpStatusCode.NoContent, "выход идемпотентен");
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/goals")]
    [InlineData("/api/tasks")]
    [InlineData("/api/finance/transactions")]
    [InlineData("/api/health/logs")]
    [InlineData("/api/files")]
    [InlineData("/api/dashboard")]
    [InlineData("/api/recommendations")]
    [InlineData("/api/study/materials")]
    [InlineData("/api/career/profile")]
    public async Task Защищённый_маршрут_без_токена_отвечает_401(string route)
    {
        var anonymous = _api.CreateClient();

        var response = await anonymous.GetAsync(route);

        // Проверка идёт по всем модулям сразу: забытый [Authorize] на новом
        // контроллере — это открытые наружу данные всех пользователей.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Подделанный_токен_не_принимается()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();

        // Меняем последний символ подписи: полезная нагрузка та же,
        // подпись перестаёт сходиться.
        var token = user.Auth.AccessToken;
        var tampered = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Мусор_вместо_токена_не_валит_приложение()
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "не-токен-вовсе");

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "а не 500");
    }

    [Fact]
    public async Task Проверка_живости_доступна_без_авторизации()
    {
        var response = await _api.CreateClient().GetAsync("/api/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
