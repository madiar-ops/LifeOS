using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Health;

public record CreateHealthLogRequest(
    DateOnly Date,
    decimal? Weight,
    decimal? SleepHours,
    MoodLevel Mood,
    int WaterMl,
    int Steps);
