using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Goals;

public record CreateGoalRequest(
    string Title,
    string? Description,
    GoalStatus Status,
    PriorityLevel Priority,
    DateTime? Deadline);
