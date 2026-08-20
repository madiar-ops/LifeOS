using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeOS.Application.Ai;
using LifeOS.Application.DTO.Auth;
using LifeOS.Application.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Testcontainers.PostgreSql;
using Microsoft.AspNetCore.TestHost;

namespace LifeOS.IntegrationTests.Infrastructure;

/// <summary>
/// Общее окружение интеграционных тестов: контейнер с PostgreSQL и поднятое
/// в памяти приложение LifeOS.API со всем его конвейером.
///
/// Контейнер поднимается ОДИН раз на всю сборку тестов: старт PostgreSQL
/// занимает несколько секунд, и повторять его для каждого класса было бы
/// расточительно. Изоляция тестов достигается иначе — каждый тест работает
/// от собственного, только что зарегистрированного пользователя, поэтому
/// данные разных тестов физически не пересекаются и чистить БД не нужно.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    /// <summary>Ключ подписи только для тестов. Длина ≥ 32 символов — требование проверки настроек.</summary>
    public const string SigningKey = "integration-tests-signing-key-32+chars";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        // Версия образа зафиксирована: «latest» однажды поменяется и уронит
        // прогон по причине, не связанной с кодом проекта.
        .WithImage("postgres:16-alpine")
        .WithDatabase("lifeos_tests")
        .WithUsername("lifeos")
        .WithPassword("lifeos")
        .Build();

    private LifeOsApiFactory _factory = null!;

    /// <summary>
    /// Подставной AI-сервис. Настоящий FastAPI в интеграционных тестах backend
    /// не участвует: его контракт проверяется отдельно, в pytest ai-service.
    /// Здесь важно другое — что backend правильно передаёт клиенту уверенность
    /// модели и корректно ведёт себя, когда AI недоступен.
    /// </summary>
    public IAiService Ai { get; } = Substitute.For<IAiService>();

    /// <summary>Настройки сериализации, совпадающие с настройками API (enum'ы — строками).</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        _factory = new LifeOsApiFactory(_database.GetConnectionString(), SigningKey, Ai);

        // Первое обращение поднимает хост. В окружении Development приложение
        // само применяет миграции при старте — отдельного шага не требуется.
        using var warmUp = _factory.CreateClient();
        var response = await warmUp.GetAsync("/api/ping");
        response.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();

        await _database.DisposeAsync();
    }

    /// <summary>Клиент без токена — для проверки анонимного доступа.</summary>
    public HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>Доступ к сервисам приложения (например, к AppDbContext) внутри теста.</summary>
    public IServiceScope CreateScope() => _factory.Services.CreateScope();

    /// <summary>
    /// Регистрирует нового пользователя со случайным email и возвращает клиента
    /// с уже подставленным Bearer-токеном.
    /// </summary>
    public async Task<TestUser> CreateAuthenticatedUserAsync(string? password = null)
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@lifeos.test";
        var secret = password ?? "Passw0rd!";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("Тест", "Пользователь", email, secret),
            Json);

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json)
                   ?? throw new InvalidOperationException("Регистрация вернула пустой ответ.");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return new TestUser(client, auth, email, secret);
    }

    /// <summary>Стандартный ответ AI, чтобы тесты не настраивали заглушку заново в каждом методе.</summary>
    public void GivenAiReturnsFinanceForecast(decimal confidence = 0.87m, bool isConfident = true)
        => Ai.AnalyzeFinanceAsync(Arg.Any<AiContracts.FinanceAnalysisRequest>(), Arg.Any<CancellationToken>())
             .Returns(new AiContracts.AiEnvelope<AiContracts.FinanceForecast>(
                 new AiContracts.FinanceForecast(120_000m, 30_000m, "rising", "Продукты", 0.2m),
                 confidence,
                 isConfident,
                 "Прогноз построен по истории операций.",
                 new List<AiContracts.FeatureContribution> { new("Продукты", 45_000, 0.4) },
                 "finance-gbr-test"));
}

/// <summary>Аутентифицированный пользователь вместе со своим HTTP-клиентом.</summary>
public sealed record TestUser(HttpClient Client, AuthResponse Auth, string Email, string Password)
{
    public Guid Id => Auth.User.Id;
}

/// <summary>
/// Фабрика приложения. Подменяет только то, что обязано быть подменено:
/// строку подключения, секреты и внешний AI-сервис. Всё остальное —
/// маршрутизация, фильтры, middleware, проверка JWT — работает по-настоящему.
/// </summary>
internal sealed class LifeOsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _signingKey;
    private readonly IAiService _ai;

    public LifeOsApiFactory(string connectionString, string signingKey, IAiService ai)
    {
        _connectionString = connectionString;
        _signingKey = signingKey;
        _ai = ai;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Окружение Development выбрано сознательно: в нём приложение само
        // накатывает миграции при старте (MigrationExtensions) и не включает
        // перенаправление на HTTPS, которого у тестового сервера нет.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,

                ["Jwt:Key"] = _signingKey,
                ["Jwt:Issuer"] = "LifeOS.API",
                ["Jwt:Audience"] = "LifeOS.Client",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",

                // Firebase в тестах не используется: файлы уходят в локальную папку.
                ["FileStorage:ForceLocal"] = "true",
                ["FileStorage:MaxFileSizeMb"] = "10",

                // Значение проверяется при старте, но настоящий вызов не выполняется:
                // IAiService ниже заменён заглушкой.
                ["AiService:InternalApiKey"] = "integration-tests-key",
                ["AiService:BaseUrl"] = "http://ai-service.invalid"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Настоящий клиент зарегистрирован через AddHttpClient, поэтому
            // снимаются все его регистрации, а не одна.
            services.RemoveAll<IAiService>();
            services.AddScoped(_ => _ai);
        });
    }
}
