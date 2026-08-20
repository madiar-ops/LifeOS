using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeOS.API.Extensions;

/// <summary>
/// Ограничение частоты запросов. Встроенный RateLimiter из ASP.NET 8 —
/// без сторонних пакетов.
///
/// Зачем: до этого перебор паролей сдерживался только временем работы BCrypt
/// (~0.25 с на попытку). Это примерно 14 000 попыток в час с одного адреса —
/// достаточно для перебора слабого пароля.
/// </summary>
public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string AiPolicy = "ai";
    public const string GlobalPolicy = "global";

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // 429, а не 503: клиент должен понимать, что дело во частоте запросов.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                // Retry-After позволяет фронтенду показать осмысленное
                // «попробуйте через N секунд» вместо общей ошибки.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();

                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsync(
                    """
                    {
                      "status": 429,
                      "title": "Слишком много запросов",
                      "detail": "Превышен лимит запросов. Повторите попытку позже.",
                      "code": "rate_limit.exceeded"
                    }
                    """,
                    cancellationToken);
            };

            // Логин и регистрация: 5 попыток за 5 минут с одного адреса.
            // Живому пользователю этого хватает с запасом, перебор становится
            // бессмысленным.
            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0
                    }));

            // AI-эндпоинты: каждый вызов уходит в FastAPI и может стоить
            // обращения к внешнему LLM. Ограничиваем по пользователю, а не по IP.
            options.AddPolicy(AiPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetUserKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Общий предохранитель на остальной API.
            options.AddPolicy(GlobalPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetUserKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    /// <summary>
    /// Ключ разделения для анонимных запросов — IP клиента.
    ///
    /// За обратным прокси (Render) реальный адрес приходит в X-Forwarded-For,
    /// и подставляет его в RemoteIpAddress middleware ForwardedHeaders.
    /// Без него все запросы выглядели бы как один клиент, и лимит
    /// сработал бы на всех сразу.
    /// </summary>
    private static string GetClientKey(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Для аутентифицированных — идентификатор пользователя, иначе IP.</summary>
    private static string GetUserKey(HttpContext context)
        => context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
              ?? GetClientKey(context)
            : GetClientKey(context);
}
