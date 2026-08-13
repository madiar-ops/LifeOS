using LifeOS.Application.Common;

namespace LifeOS.Application.DTO.Health;

public class HealthLogQueryParams : PaginationParams
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
