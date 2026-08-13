using LifeOS.Application.Common;

namespace LifeOS.Application.DTO.Common;

public static class PagedResponseExtensions
{
    public static PagedResponse<T> ToResponse<T>(this PagedResult<T> result)
        => new(
            result.Items,
            result.TotalCount,
            result.PageNumber,
            result.PageSize,
            result.TotalPages,
            result.HasPrevious,
            result.HasNext);
}
