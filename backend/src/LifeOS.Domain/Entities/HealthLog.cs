using LifeOS.Domain.Common;
using LifeOS.Domain.Enums;

namespace LifeOS.Domain.Entities;

/// <summary>
/// Ежедневная запись здоровья. Служит обучающим/входным датасетом
/// для health-прогноза AI-сервиса.
/// </summary>
public class HealthLog : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Вес, кг.</summary>
    public decimal? Weight { get; set; }

    /// <summary>Продолжительность сна, часы.</summary>
    public decimal? SleepHours { get; set; }

    public MoodLevel Mood { get; set; } = MoodLevel.Neutral;

    /// <summary>Выпито воды, мл.</summary>
    public int WaterMl { get; set; }

    public int Steps { get; set; }

    /// <summary>Дата записи. Одна запись на пользователя в день (уникальный индекс).</summary>
    public DateOnly Date { get; set; }

    public User User { get; set; } = null!;
}
