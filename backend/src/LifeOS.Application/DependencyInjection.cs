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

        return services;
    }
}
