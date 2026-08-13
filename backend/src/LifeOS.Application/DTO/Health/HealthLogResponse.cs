using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Health;

public record HealthLogResponse(
    Guid Id,
    DateOnly Date,
    decimal? Weight,
    decimal? SleepHours,
    MoodLevel Mood,
    int WaterMl,
    int Steps,
    DateTime CreatedAt);
