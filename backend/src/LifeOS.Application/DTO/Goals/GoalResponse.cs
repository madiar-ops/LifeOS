using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Goals;

public record GoalResponse(
    Guid Id,
    string Title,
    string? Description,
    GoalStatus Status,
    PriorityLevel Priority,
    DateTime? Deadline,
    int TotalTasks,
    int CompletedTasks,
    DateTime CreatedAt,
    DateTime UpdatedAt);
