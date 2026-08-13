using LifeOS.Application.Common;
using LifeOS.Application.DTO.Health;

namespace LifeOS.Application.Interfaces.Services;

public interface IHealthLogService
{
    Task<PagedResult<HealthLogResponse>> GetAllAsync(HealthLogQueryParams query, CancellationToken cancellationToken = default);
    Task<HealthLogResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HealthLogResponse> CreateAsync(CreateHealthLogRequest request, CancellationToken cancellationToken = default);
    Task<HealthLogResponse> UpdateAsync(Guid id, UpdateHealthLogRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
