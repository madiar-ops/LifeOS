using LifeOS.Domain.Common;
using LifeOS.Domain.Enums;

namespace LifeOS.Domain.Entities;

/// <summary>Цель пользователя. Может содержать задачи (Tasks).</summary>
public class Goal : BaseEntity, IAuditableEntity
{
    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public GoalStatus Status { get; set; } = GoalStatus.NotStarted;
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public DateTime? Deadline { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
