using System.Text.Json;

namespace LifeOS.IntegrationTests.Infrastructure;

/// <summary>
/// Разбор ответа об ошибке в формате ProblemDetails (RFC 7807) вместе
/// с расширениями проекта: машиночитаемым <c>code</c> и <c>traceId</c>.
///
/// Тесты проверяют именно эти поля, а не текст сообщения: фронтенд принимает
/// решения по коду, и сообщение может быть переписано без ломающих изменений.
/// </summary>
internal static class ProblemReader
{
    public static async Task<ApiProblem> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException(
                $"Ответ {(int)response.StatusCode} пришёл с пустым телом — ProblemDetails ожидался, но не отдан.");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("errors", out var errorsElement)
            && errorsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in errorsElement.EnumerateObject())
            {
                errors[field.Name] = field.Value.EnumerateArray()
                    .Select(message => message.GetString() ?? string.Empty)
                    .ToArray();
            }
        }

        return new ApiProblem(
            GetString(root, "title"),
            GetString(root, "detail"),
            GetString(root, "code"),
            GetString(root, "traceId"),
            errors);
    }

    private static string? GetString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <param name="Errors">Словарь «поле → сообщения» из ValidationProblemDetails. Пуст, если ошибка не по полям.</param>
internal sealed record ApiProblem(
    string? Title,
    string? Detail,
    string? Code,
    string? TraceId,
    IReadOnlyDictionary<string, string[]> Errors);
