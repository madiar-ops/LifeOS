namespace LifeOS.Domain.Common;

/// <summary>
/// Базовый класс для всех сущностей домена.
/// Id — UUID, генерируется приложением (не БД): сущность полностью валидна
/// ещё до вызова SaveChanges, что упрощает тесты и работу с графами объектов.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Проставляется автоматически (AuditableEntityInterceptor). Всегда UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
