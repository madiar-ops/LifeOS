using LifeOS.Domain.Common;
using LifeOS.Domain.Enums;

namespace LifeOS.Domain.Entities;

/// <summary>
/// Рекомендация AI пользователю. Confidence реализует принцип MASTER_GUIDE:
/// «если AI не уверен — он сообщает об этом».
/// </summary>
public class Recommendation : BaseEntity
{
    public Guid UserId { get; set; }

    public ModuleType Module { get; set; }
    public string Content { get; set; } = null!;

    /// <summary>Уверенность модели, 0.00–1.00.</summary>
    public decimal Confidence { get; set; }

    public User User { get; set; } = null!;
}
