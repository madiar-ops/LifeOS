namespace LifeOS.Application.DTO.Tasks;

public record UpdateTaskRequest(string Title, Guid? GoalId, bool Completed, DateTime? Deadline);
