using LifeOS.Domain.Common;

namespace LifeOS.Domain.Entities;

/// <summary>Карьерный профиль пользователя. Связь 1:1 с User.</summary>
public class CareerProfile : BaseEntity, IAuditableEntity
{
    public Guid UserId { get; set; }

    /// <summary>Ссылка на загруженное резюме в Firebase (через таблицу Files).</summary>
    public Guid? ResumeFileId { get; set; }

    /// <summary>Навыки, перечисленные через запятую.</summary>
    public string? Skills { get; set; }

    public string? DesiredPosition { get; set; }

    /// <summary>Текстовый анализ резюме от AI-сервиса.</summary>
    public string? AiReview { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public StoredFile? ResumeFile { get; set; }
}
