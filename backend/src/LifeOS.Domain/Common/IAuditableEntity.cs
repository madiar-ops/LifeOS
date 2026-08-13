namespace LifeOS.Domain.Common;

/// <summary>
/// Помечает сущности, которые отслеживают дату последнего изменения.
/// Значение проставляется автоматически в AuditableEntityInterceptor.
/// </summary>
public interface IAuditableEntity
{
    DateTime UpdatedAt { get; set; }
}
