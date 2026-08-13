namespace LifeOS.Application.DTO.Tasks;

public record TaskResponse(
    Guid Id,
    Guid? GoalId,
    string? GoalTitle,
    string Title,
    bool Completed,
    DateTime? Deadline,
    DateTime CreatedAt,
    DateTime UpdatedAt);
