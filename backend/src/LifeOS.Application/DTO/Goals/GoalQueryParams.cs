using LifeOS.Application.Common;
using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Goals;

/// <summary>Фильтры списка целей. Наследует ограничения пагинации.</summary>
public class GoalQueryParams : PaginationParams
{
    public GoalStatus? Status { get; set; }
    public PriorityLevel? Priority { get; set; }

    /// <summary>Поиск по названию (без учёта регистра).</summary>
    public string? Search { get; set; }
}
