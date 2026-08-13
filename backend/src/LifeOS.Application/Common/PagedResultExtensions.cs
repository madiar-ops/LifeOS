namespace LifeOS.Application.Common;

public static class PagedResultExtensions
{
    /// <summary>
    /// Преобразует страницу сущностей в страницу DTO, сохраняя метаданные пагинации.
    /// Наружу не должны выходить доменные сущности — только DTO.
    /// </summary>
    public static PagedResult<TDto> Map<TSource, TDto>(
        this PagedResult<TSource> source,
        Func<TSource, TDto> selector)
        => new(
            source.Items.Select(selector).ToList(),
            source.TotalCount,
            source.PageNumber,
            source.PageSize);
}
