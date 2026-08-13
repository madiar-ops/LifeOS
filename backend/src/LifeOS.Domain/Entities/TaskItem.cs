using LifeOS.Domain.Common;

namespace LifeOS.Domain.Entities;

/// <summary>
/// Задача пользователя. Класс назван TaskItem, а не Task, чтобы не конфликтовать
/// с System.Threading.Tasks.Task. В БД таблица называется "Tasks".
///
/// UserId обязателен, GoalId опционален: задача может существовать самостоятельно,
/// без привязки к цели (требование Dashboard «Количество задач»).
/// </summary>
public class TaskItem : BaseEntity, IAuditableEntity
{
    public Guid UserId { get; set; }
    public Guid? GoalId { get; set; }

    public string Title { get; set; } = null!;
    public bool Completed { get; set; }
    public DateTime? Deadline { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Goal? Goal { get; set; }
}
