using LifeOS.Application.Common;

namespace LifeOS.Application.DTO.Tasks;

public class TaskQueryParams : PaginationParams
{
    public bool? Completed { get; set; }
    public Guid? GoalId { get; set; }

    /// <summary>Только задачи с дедлайном не позже указанной даты.</summary>
    public DateTime? DueBefore { get; set; }

    public string? Search { get; set; }
}
