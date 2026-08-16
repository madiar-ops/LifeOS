using LifeOS.Application.Interfaces.Auth;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Infrastructure.Auth;
using LifeOS.Application.Common;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Data.Interceptors;
using LifeOS.Infrastructure.Ai;
using LifeOS.Infrastructure.Repositories;
using LifeOS.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LifeOS.Infrastructure;

/// <summary>
/// Регистрация инфраструктуры: БД, репозитории, вспомогательные сервисы.
/// Слой API не знает ни про EF Core, ни про Npgsql — только про этот метод.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Строка подключения 'ConnectionStrings:DefaultConnection' не задана. " +
                "Задайте её в user-secrets (dev) или в переменных окружения (prod).");

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                // Neon — облачная БД: сетевые сбои штатны, поэтому включаем retry.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());

            if (environment.IsDevelopment())
            {
                // Значения параметров в логах — только в dev: в проде это утечка данных.
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Auth: обе реализации не хранят состояние, поэтому Singleton.
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // Провайдер хранилища выбирается по конфигурации: если bucket не задан
        // (или явно включён ForceLocal) — работаем с локальной папкой.
        // Так разработка модулей не блокируется настройкой Firebase.
        var storageSettings = configuration
            .GetSection(FileStorageSettings.SectionName)
            .Get<FileStorageSettings>() ?? new FileStorageSettings();

        if (storageSettings.UseLocal)
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        else
            services.AddSingleton<IFileStorageService, FirebaseStorageService>();

        services.AddScoped<IDocumentTextExtractor, PdfTextExtractor>();

        // Типизированный клиент AI-сервиса. Базовый адрес и внутренний ключ
        // задаются один раз здесь — сам клиент их не знает.
        var aiSettings = configuration.GetSection(AiSettings.SectionName).Get<AiSettings>()
                         ?? new AiSettings();

        services.AddHttpClient<IAiService, AiServiceClient>(client =>
        {
            client.BaseAddress = new Uri(aiSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", aiSettings.InternalApiKey);
        })
        // Стандартный набор устойчивости: ретраи с экспоненциальной задержкой,
        // circuit breaker и таймаут. Сетевой сбой между двумя сервисами —
        // штатная ситуация, а не повод показать пользователю ошибку.
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds);

            // Таймаут всей цепочки попыток обязан быть больше таймаута одной,
            // иначе библиотека отклонит конфигурацию при старте.
            options.TotalRequestTimeout.Timeout =
                TimeSpan.FromSeconds(aiSettings.TimeoutSeconds * 3);

            options.CircuitBreaker.SamplingDuration =
                TimeSpan.FromSeconds(aiSettings.TimeoutSeconds * 2);
        });

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres");

        return services;
    }
}
