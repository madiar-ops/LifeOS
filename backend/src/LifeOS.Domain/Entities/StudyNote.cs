using LifeOS.Domain.Common;

namespace LifeOS.Domain.Entities;

/// <summary>Заметка пользователя к учебному материалу.</summary>
public class StudyNote : BaseEntity, IAuditableEntity
{
    public Guid UserId { get; set; }
    public Guid StudyMaterialId { get; set; }

    public string Content { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public StudyMaterial StudyMaterial { get; set; } = null!;
}
