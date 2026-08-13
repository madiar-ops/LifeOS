using System.Text.Json;
using LifeOS.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Middleware;

/// <summary>
/// Единая точка обработки исключений. Контроллеры и сервисы не содержат
/// try/catch — они просто бросают доменные исключения, а middleware
/// превращает их в корректный HTTP-ответ формата ProblemDetails (RFC 7807).
///
/// Внутренние детали (stack trace) наружу уходят только в Development.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, code) = exception switch
        {
            NotFoundException e      => (StatusCodes.Status404NotFound, "Ресурс не найден", e.Code),
            ConflictException e      => (StatusCodes.Status409Conflict, "Конфликт данных", e.Code),
            ForbiddenException e     => (StatusCodes.Status403Forbidden, "Доступ запрещён", e.Code),
            BusinessRuleException e  => (StatusCodes.Status400BadRequest, "Нарушено бизнес-правило", e.Code),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Требуется авторизация", "auth.unauthorized"),
            OperationCanceledException  => (499, "Запрос отменён клиентом", "request.cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера", "server.error")
        };

        // Ожидаемые доменные ошибки — Warning, всё остальное — Error с полным стеком.
        if (statusCode >= 500)
            _logger.LogError(exception, "Необработанное исключение при {Method} {Path}",
                context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("Обработанная ошибка {Code} при {Method} {Path}: {Message}",
                code, context.Request.Method, context.Request.Path, exception.Message);

        // Если ответ уже начал отправляться, заголовки менять нельзя — только логируем.
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Ответ уже начат — ProblemDetails отправить невозможно.");
            return;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode >= 500 && !_environment.IsDevelopment()
                ? "Произошла непредвиденная ошибка. Обратитесь к администратору."
                : exception.Message,
            Instance = context.Request.Path
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (_environment.IsDevelopment() && statusCode >= 500)
            problem.Extensions["stackTrace"] = exception.StackTrace;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

/// <summary>Регистрация middleware в конвейере одной строкой.</summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
