using LifeOS.Application.Common;
using LifeOS.Application.DTO.Goals;

namespace LifeOS.Application.Interfaces.Services;

public interface IGoalService
{
    Task<PagedResult<GoalResponse>> GetAllAsync(GoalQueryParams query, CancellationToken cancellationToken = default);
    Task<GoalResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoalResponse> CreateAsync(CreateGoalRequest request, CancellationToken cancellationToken = default);
    Task<GoalResponse> UpdateAsync(Guid id, UpdateGoalRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
