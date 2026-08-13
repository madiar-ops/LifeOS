namespace LifeOS.Application.Common;

/// <summary>
/// Параметры постраничного вывода. PageSize жёстко ограничен сверху,
/// чтобы клиент не мог запросить всю таблицу одним вызовом.
/// </summary>
public class PaginationParams
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;
    private int _pageNumber = 1;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 1,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public int Skip => (PageNumber - 1) * PageSize;
}
