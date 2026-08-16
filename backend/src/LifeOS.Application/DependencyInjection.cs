using FluentValidation;
using LifeOS.Application.Common;
using LifeOS.Application.Interfaces.Auth;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeOS.Application;

/// <summary>Регистрация сервисов слоя Application.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Настройки JWT валидируются при старте, а не при первом запросе:
        // пустой ключ — это отказ приложения подняться, а не 500 в проде.
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Key),
                "Jwt:Key не задан. Укажите его в user-secrets или переменных окружения.")
            .Validate(s => s.Key.Length >= 32,
                "Jwt:Key должен быть не короче 32 символов (256 бит для HMAC-SHA256).")
            .Validate(s => s.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes должен быть больше нуля.")
            .Validate(s => s.RefreshTokenDays > 0, "Jwt:RefreshTokenDays должен быть больше нуля.")
            .ValidateOnStart();

        services.AddOptions<FileStorageSettings>()
            .Bind(configuration.GetSection(FileStorageSettings.SectionName))
            .Validate(s => s.MaxFileSizeMb is > 0 and <= 100,
                "FileStorage:MaxFileSizeMb должен быть в диапазоне 1–100.")
            .ValidateOnStart();

        services.AddOptions<AiSettings>()
            .Bind(configuration.GetSection(AiSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.BaseUrl),
                "AiService:BaseUrl не задан.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.InternalApiKey),
                "AiService:InternalApiKey не задан. Он должен совпадать с INTERNAL_API_KEY в ai-service/.env.")
            .ValidateOnStart();

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(assembly);

        // Все сервисы Scoped: они работают с UnitOfWork, живущим в рамках запроса.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IHealthLogService, HealthLogService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IAiHistoryRecorder, AiHistoryRecorder>();
        services.AddScoped<IStudyService, StudyService>();
        services.AddScoped<ICareerService, CareerService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
