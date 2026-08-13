namespace LifeOS.Application.DTO.Tasks;

/// <summary>GoalId опционален: задача может быть самостоятельной.</summary>
public record CreateTaskRequest(string Title, Guid? GoalId, DateTime? Deadline);
