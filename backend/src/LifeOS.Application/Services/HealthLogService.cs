using AutoMapper;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Health;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Application.Services;

public class HealthLogService : IHealthLogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IMapper _mapper;

    public HealthLogService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _mapper = mapper;
    }

    public async Task<PagedResult<HealthLogResponse>> GetAllAsync(
        HealthLogQueryParams query, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var source = _unitOfWork.HealthLogs.Query().Where(h => h.UserId == userId);

        if (query.From.HasValue) source = source.Where(h => h.Date >= query.From.Value);
        if (query.To.HasValue) source = source.Where(h => h.Date <= query.To.Value);

        var totalCount = await source.CountAsync(cancellationToken);

        // Сортировка по дате убыванием: это временной ряд, свежее — важнее.
        var items = await source
            .OrderByDescending(h => h.Date)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<HealthLog>(items, totalCount, query.PageNumber, query.PageSize)
            .Map(_mapper.Map<HealthLogResponse>);
    }

    public async Task<HealthLogResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _mapper.Map<HealthLogResponse>(await LoadOwnedAsync(id, tracked: false, cancellationToken));

    public async Task<HealthLogResponse> CreateAsync(
        CreateHealthLogRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        if (request.Date > _dateTime.Today)
            throw new BusinessRuleException(
                "Нельзя создать запись здоровья будущей датой.", "health.future_date");

        // Проверяем заранее, чтобы вернуть понятную 409 вместо падения
        // на уникальном индексе (UserId, Date) из Фазы 1.
        var exists = await _unitOfWork.HealthLogs.AnyAsync(
            h => h.UserId == userId && h.Date == request.Date, cancellationToken);

        if (exists)
            throw new ConflictException(
                $"Запись за {request.Date:yyyy-MM-dd} уже существует. Отредактируйте её.",
                "health.duplicate_date");

        var log = new HealthLog
        {
            UserId = userId,
            Date = request.Date,
            Weight = request.Weight,
            SleepHours = request.SleepHours,
            Mood = request.Mood,
            WaterMl = request.WaterMl,
            Steps = request.Steps
        };

        await _unitOfWork.HealthLogs.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<HealthLogResponse>(log);
    }

    /// <summary>Дата не меняется — она часть уникального ключа (UserId, Date).</summary>
    public async Task<HealthLogResponse> UpdateAsync(
        Guid id, UpdateHealthLogRequest request, CancellationToken cancellationToken = default)
    {
        var log = await LoadOwnedAsync(id, tracked: true, cancellationToken);

        log.Weight = request.Weight;
        log.SleepHours = request.SleepHours;
        log.Mood = request.Mood;
        log.WaterMl = request.WaterMl;
        log.Steps = request.Steps;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<HealthLogResponse>(log);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var log = await LoadOwnedAsync(id, tracked: true, cancellationToken);

        _unitOfWork.HealthLogs.Remove(log);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<HealthLog> LoadOwnedAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var log = await _unitOfWork.HealthLogs.Query(asNoTracking: !tracked)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(log, log?.UserId ?? Guid.Empty, userId, nameof(HealthLog), id);
    }
}
