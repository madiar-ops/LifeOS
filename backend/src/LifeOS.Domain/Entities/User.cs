using LifeOS.Domain.Common;
using LifeOS.Domain.Enums;

namespace LifeOS.Domain.Entities;

/// <summary>Учётная запись пользователя. Email уникален, пароль хранится только как BCrypt-хеш.</summary>
public class User : BaseEntity, IAuditableEntity
{
    public string Name { get; set; } = null!;
    public string Surname { get; set; } = null!;

    /// <summary>Хранится в нижнем регистре — уникальность нечувствительна к регистру.</summary>
    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    /// <summary>Ссылка на аватар в Firebase Storage.</summary>
    public string? AvatarUrl { get; set; }

    public UserRole Role { get; set; } = UserRole.User;

    public DateTime UpdatedAt { get; set; }

    // Навигационные свойства
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<HealthLog> HealthLogs { get; set; } = new List<HealthLog>();
    public ICollection<StudyMaterial> StudyMaterials { get; set; } = new List<StudyMaterial>();
    public ICollection<StudyNote> StudyNotes { get; set; } = new List<StudyNote>();
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
    public ICollection<AiHistoryEntry> AiHistory { get; set; } = new List<AiHistoryEntry>();
    public ICollection<StoredFile> Files { get; set; } = new List<StoredFile>();
    public CareerProfile? CareerProfile { get; set; }
}
