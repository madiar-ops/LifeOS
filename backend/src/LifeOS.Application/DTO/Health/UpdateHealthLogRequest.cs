using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Health;

/// <summary>Дата записи не меняется: она — часть уникального ключа (UserId, Date).</summary>
public record UpdateHealthLogRequest(
    decimal? Weight,
    decimal? SleepHours,
    MoodLevel Mood,
    int WaterMl,
    int Steps);
