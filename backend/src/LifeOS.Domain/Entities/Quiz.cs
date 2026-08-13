using LifeOS.Domain.Common;

namespace LifeOS.Domain.Entities;

/// <summary>
/// Тест, сгенерированный AI по учебному материалу.
/// Questions хранится как jsonb: структура вопросов может меняться,
/// а отдельная таблица вопросов для MVP избыточна.
/// </summary>
public class Quiz : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid StudyMaterialId { get; set; }

    /// <summary>JSON-массив вопросов с вариантами ответов (jsonb в PostgreSQL).</summary>
    public string Questions { get; set; } = "[]";

    /// <summary>Количество правильных ответов. Null — тест ещё не пройден.</summary>
    public int? Score { get; set; }

    public int TotalQuestions { get; set; }

    public User User { get; set; } = null!;
    public StudyMaterial StudyMaterial { get; set; } = null!;
}
