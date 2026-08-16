using AutoMapper;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Application.Services;

public class RecommendationService : IRecommendationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public RecommendationService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<PagedResult<RecommendationResponse>> GetAllAsync(
        PaginationParams pagination, ModuleType? module, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var source = _unitOfWork.Recommendations.Query().Where(r => r.UserId == userId);

        if (module.HasValue)
            source = source.Where(r => r.Module == module.Value);

        var totalCount = await source.CountAsync(cancellationToken);

        // Сначала самые уверенные и свежие: пользователь видит наверху то,
        // в чём модель уверена больше всего.
        var items = await source
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Confidence)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Recommendation>(items, totalCount, pagination.PageNumber, pagination.PageSize)
            .Map(_mapper.Map<RecommendationResponse>);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var recommendation = await _unitOfWork.Recommendations.Query(asNoTracking: false)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        var owned = CrudGuard.EnsureOwned(
            recommendation, recommendation?.UserId ?? Guid.Empty, userId, nameof(Recommendation), id);

        _unitOfWork.Recommendations.Remove(owned);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AiHistoryResponse>> GetHistoryAsync(
        PaginationParams pagination, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var source = _unitOfWork.AiHistory.Query().Where(a => a.UserId == userId);

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(a => a.CreatedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        // Payload наружу не отдаём: там могут быть фрагменты личных документов.
        return new PagedResult<AiHistoryEntry>(items, totalCount, pagination.PageNumber, pagination.PageSize)
            .Map(entry => new AiHistoryResponse(entry.Id, entry.Endpoint, entry.Confidence, entry.CreatedAt));
    }
}
