using LifeOS.Application.Common;
using LifeOS.Application.DTO.Tasks;

namespace LifeOS.Application.Interfaces.Services;

public interface ITaskService
{
    Task<PagedResult<TaskResponse>> GetAllAsync(TaskQueryParams query, CancellationToken cancellationToken = default);
    Task<TaskResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskResponse> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskResponse> ToggleCompleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
