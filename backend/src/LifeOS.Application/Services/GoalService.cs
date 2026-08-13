using AutoMapper;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Goals;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Application.Services;

public class GoalService : IGoalService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GoalService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<PagedResult<GoalResponse>> GetAllAsync(
        GoalQueryParams query, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Фильтр по UserId применяется ПЕРВЫМ и всегда — чужие цели
        // не могут попасть в выборку ни при каком наборе параметров.
        var source = _unitOfWork.Goals.Query()
            .Where(g => g.UserId == userId);

        if (query.Status.HasValue)
            source = source.Where(g => g.Status == query.Status.Value);

        if (query.Priority.HasValue)
            source = source.Where(g => g.Priority == query.Priority.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // ToLower().Contains() EF транслирует в SQL LOWER(...) LIKE '%...%'.
            // Намеренно не используем Npgsql-специфичный EF.Functions.ILike:
            // слой Application не должен знать, какая именно СУБД под ним.
            var pattern = query.Search.Trim().ToLower();
            source = source.Where(g => g.Title.ToLower().Contains(pattern));
        }

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .Include(g => g.Tasks)
            .OrderByDescending(g => g.Priority)
            .ThenBy(g => g.Deadline ?? DateTime.MaxValue)
            .ThenByDescending(g => g.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Goal>(items, totalCount, query.PageNumber, query.PageSize)
            .Map(_mapper.Map<GoalResponse>);
    }

    public async Task<GoalResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await LoadOwnedWithTasksAsync(id, cancellationToken);
        return _mapper.Map<GoalResponse>(goal);
    }

    public async Task<GoalResponse> CreateAsync(
        CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        var goal = new Goal
        {
            UserId = _currentUser.GetRequiredUserId(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = request.Status,
            Priority = request.Priority,
            Deadline = request.Deadline
        };

        await _unitOfWork.Goals.AddAsync(goal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<GoalResponse>(goal);
    }

    public async Task<GoalResponse> UpdateAsync(
        Guid id, UpdateGoalRequest request, CancellationToken cancellationToken = default)
    {
        var goal = await LoadOwnedTrackedAsync(id, cancellationToken);

        goal.Title = request.Title.Trim();
        goal.Description = request.Description?.Trim();
        goal.Status = request.Status;
        goal.Priority = request.Priority;
        goal.Deadline = request.Deadline;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Перечитываем с задачами: цель загружалась без Include(Tasks),
        // иначе счётчики TotalTasks/CompletedTasks вернулись бы нулевыми.
        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Удаление цели. Связанные задачи НЕ удаляются: у них GoalId станет NULL
    /// (правило ON DELETE SET NULL из Фазы 1) — задача переживает свою цель.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await LoadOwnedTrackedAsync(id, cancellationToken);

        _unitOfWork.Goals.Remove(goal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---- Вспомогательные методы -----------------------------------------

    private async Task<Goal> LoadOwnedTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var goal = await _unitOfWork.Goals.Query(asNoTracking: false)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(goal, goal?.UserId ?? Guid.Empty, userId, nameof(Goal), id);
    }

    private async Task<Goal> LoadOwnedWithTasksAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var goal = await _unitOfWork.Goals.Query()
            .Include(g => g.Tasks)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(goal, goal?.UserId ?? Guid.Empty, userId, nameof(Goal), id);
    }
}
