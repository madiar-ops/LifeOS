namespace LifeOS.Application.DTO.Common;

/// <summary>
/// Страница данных в том виде, в каком её видит клиент.
/// Отдельный тип от PagedResult нужен, чтобы форма JSON-ответа
/// была явно зафиксирована и не менялась вслед за внутренним классом.
/// </summary>
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);
