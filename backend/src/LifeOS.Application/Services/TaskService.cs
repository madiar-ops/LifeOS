using AutoMapper;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Tasks;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Application.Services;

public class TaskService : ITaskService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public TaskService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<PagedResult<TaskResponse>> GetAllAsync(
        TaskQueryParams query, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var source = _unitOfWork.Tasks.Query()
            .Where(t => t.UserId == userId);

        if (query.Completed.HasValue)
            source = source.Where(t => t.Completed == query.Completed.Value);

        if (query.GoalId.HasValue)
            source = source.Where(t => t.GoalId == query.GoalId.Value);

        if (query.DueBefore.HasValue)
            source = source.Where(t => t.Deadline != null && t.Deadline <= query.DueBefore.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = query.Search.Trim().ToLower();
            source = source.Where(t => t.Title.ToLower().Contains(pattern));
        }

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .Include(t => t.Goal)
            // Невыполненные — выше; внутри группы ближайший дедлайн — первым.
            .OrderBy(t => t.Completed)
            .ThenBy(t => t.Deadline ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskItem>(items, totalCount, query.PageNumber, query.PageSize)
            .Map(_mapper.Map<TaskResponse>);
    }

    public async Task<TaskResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var task = await _unitOfWork.Tasks.Query()
            .Include(t => t.Goal)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        var owned = CrudGuard.EnsureOwned(task, task?.UserId ?? Guid.Empty, userId, nameof(TaskItem), id);
        return _mapper.Map<TaskResponse>(owned);
    }

    public async Task<TaskResponse> CreateAsync(
        CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Привязать задачу можно только к СВОЕЙ цели — иначе через GoalId
        // можно было бы засорять чужие цели своими задачами.
        await EnsureGoalOwnedAsync(request.GoalId, userId, cancellationToken);

        var task = new TaskItem
        {
            UserId = userId,
            GoalId = request.GoalId,
            Title = request.Title.Trim(),
            Deadline = request.Deadline,
            Completed = false
        };

        await _unitOfWork.Tasks.AddAsync(task, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(task.Id, cancellationToken);
    }

    public async Task<TaskResponse> UpdateAsync(
        Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var task = await LoadOwnedTrackedAsync(id, cancellationToken);

        await EnsureGoalOwnedAsync(request.GoalId, userId, cancellationToken);

        task.Title = request.Title.Trim();
        task.GoalId = request.GoalId;
        task.Completed = request.Completed;
        task.Deadline = request.Deadline;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(task.Id, cancellationToken);
    }

    /// <summary>Переключение статуса — отдельный endpoint для чекбокса в UI.</summary>
    public async Task<TaskResponse> ToggleCompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await LoadOwnedTrackedAsync(id, cancellationToken);

        task.Completed = !task.Completed;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(task.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await LoadOwnedTrackedAsync(id, cancellationToken);

        _unitOfWork.Tasks.Remove(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---- Вспомогательные методы -----------------------------------------

    private async Task<TaskItem> LoadOwnedTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var task = await _unitOfWork.Tasks.Query(asNoTracking: false)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(task, task?.UserId ?? Guid.Empty, userId, nameof(TaskItem), id);
    }

    private async Task EnsureGoalOwnedAsync(Guid? goalId, Guid userId, CancellationToken cancellationToken)
    {
        if (!goalId.HasValue) return;

        var exists = await _unitOfWork.Goals.AnyAsync(
            g => g.Id == goalId.Value && g.UserId == userId, cancellationToken);

        if (!exists)
            throw new NotFoundException(nameof(Goal), goalId.Value);
    }
}
