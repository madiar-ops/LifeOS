using LifeOS.Domain.Common;

namespace LifeOS.Domain.Entities;

/// <summary>
/// Учебный материал: загруженный PDF (через FileId) + AI-summary.
/// Сам файл лежит в Firebase Storage, в БД — только метаданные.
/// </summary>
public class StudyMaterial : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid FileId { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>Краткое содержание, сгенерированное AI-сервисом. Null, пока не сгенерировано.</summary>
    public string? Summary { get; set; }

    public User User { get; set; } = null!;
    public StoredFile File { get; set; } = null!;

    public ICollection<StudyNote> Notes { get; set; } = new List<StudyNote>();
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
}
