using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LifeOS.API.Filters;

/// <summary>
/// Автоматически прогоняет входящие DTO через FluentValidation до входа в контроллер.
/// Благодаря этому в контроллерах нет ни одной проверки вида if (!ModelState.IsValid).
/// Ошибки отдаются в формате ValidationProblemDetails — фронт получает
/// словарь "поле → список ошибок" и может подсветить конкретные инпуты.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationFilter(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Штатный ответ на невалидный ModelState подавлен в Program.cs, поэтому
        // ошибки привязки (битый JSON, несуществующее значение enum, буквы вместо
        // числа) нужно перехватить здесь. Иначе в действие пришёл бы null
        // и мы получили бы 500 вместо понятной 400.
        if (!context.ModelState.IsValid)
        {
            var bindingErrors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors
                        .Select(err => string.IsNullOrWhiteSpace(err.ErrorMessage)
                            ? "Некорректное значение."
                            : err.ErrorMessage)
                        .ToArray());

            context.Result = BuildProblem(context, bindingErrors);
            return;
        }

        var errors = new Dictionary<string, List<string>>();

        foreach (var argument in context.ActionArguments)
        {
            if (argument.Value is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.Value.GetType());

            if (_serviceProvider.GetService(validatorType) is not IValidator validator) continue;

            var validationContext = new ValidationContext<object>(argument.Value);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (result.IsValid) continue;

            foreach (var failure in result.Errors)
            {
                if (!errors.TryGetValue(failure.PropertyName, out var messages))
                    errors[failure.PropertyName] = messages = new List<string>();

                messages.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            context.Result = BuildProblem(
                context, errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
            return;
        }

        await next();
    }

    /// <summary>
    /// Единый формат ответа на любую ошибку валидации: словарь «поле → ошибки».
    /// Фронт подсвечивает конкретные инпуты, не разбирая текст сообщения.
    /// </summary>
    private static BadRequestObjectResult BuildProblem(
        ActionExecutingContext context, IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Ошибка валидации",
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["code"] = "validation.failed";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem);
    }
}
