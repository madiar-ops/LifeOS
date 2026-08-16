using LifeOS.Application.Common;
using LifeOS.Application.DTO.Ai;
using LifeOS.Domain.Enums;

namespace LifeOS.Application.Interfaces.Services;

public interface IRecommendationService
{
    Task<PagedResult<RecommendationResponse>> GetAllAsync(
        PaginationParams pagination, ModuleType? module, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<AiHistoryResponse>> GetHistoryAsync(
        PaginationParams pagination, CancellationToken cancellationToken = default);
}
