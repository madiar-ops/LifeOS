namespace LifeOS.API.Extensions;

public static class CorsExtensions
{
    public const string PolicyName = "LifeOSCors";

    /// <summary>
    /// Список разрешённых origin задаётся конфигурацией (Cors:AllowedOrigins),
    /// а не хардкодом: у dev, preview и prod фронтенда разные адреса.
    /// AllowAnyOrigin намеренно не используется — с credentials он несовместим и небезопасен.
    /// </summary>
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? new[] { "http://localhost:5173" };

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        return services;
    }
}
