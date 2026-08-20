using Microsoft.AspNetCore.HttpOverrides;

namespace LifeOS.API.Extensions;

public static class ProductionExtensions
{
    /// <summary>
    /// Обработка заголовков обратного прокси.
    ///
    /// На Render (как и на большинстве PaaS) TLS терминируется балансировщиком,
    /// а до приложения запрос доходит по HTTP. Без этого middleware:
    /// • Request.Scheme всегда "http" → ссылки в ответах получаются http;
    /// • RemoteIpAddress — адрес прокси, а не клиента → ограничение частоты
    ///   запросов считало бы всех пользователей одним клиентом;
    /// • UseHttpsRedirection уходил бы в бесконечный цикл редиректов.
    /// </summary>
    public static IServiceCollection AddProxyHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Сети прокси заранее неизвестны (адреса Render меняются),
            // поэтому списки доверенных узлов очищаем. Это приемлемо,
            // потому что приложение недоступно в обход балансировщика.
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>
    /// Заголовки безопасности. Дёшево и закрывает базовый набор атак,
    /// про который спрашивают на защите.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            // Запрет угадывания типа содержимого: без него браузер может
            // исполнить загруженный файл как скрипт.
            headers["X-Content-Type-Options"] = "nosniff";

            // Защита от кликджекинга: страницу нельзя встроить в чужой iframe.
            headers["X-Frame-Options"] = "DENY";

            // Не отдавать полный URL нашего API на сторонние домены.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // API не нуждается ни в камере, ни в микрофоне, ни в геолокации.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            await next();
        });
}
