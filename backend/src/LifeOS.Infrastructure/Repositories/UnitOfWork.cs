using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace LifeOS.Infrastructure.Repositories;

/// <summary>
/// Unit of Work: все репозитории разделяют один AppDbContext,
/// поэтому изменения фиксируются одной транзакцией одним SaveChangesAsync.
/// Репозитории создаются лениво — неиспользуемые в запросе не создаются вовсе.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IDateTimeProvider _dateTime;
    private IDbContextTransaction? _transaction;

    private IUserRepository? _users;
    private IRefreshTokenRepository? _refreshTokens;
    private IGenericRepository<Goal>? _goals;
    private IGenericRepository<TaskItem>? _tasks;
    private IGenericRepository<Transaction>? _transactions;
    private IGenericRepository<HealthLog>? _healthLogs;
    private IGenericRepository<StudyMaterial>? _studyMaterials;
    private IGenericRepository<StudyNote>? _studyNotes;
    private IGenericRepository<Quiz>? _quizzes;
    private IGenericRepository<CareerProfile>? _careerProfiles;
    private IGenericRepository<Recommendation>? _recommendations;
    private IGenericRepository<AiHistoryEntry>? _aiHistory;
    private IGenericRepository<StoredFile>? _files;

    public UnitOfWork(AppDbContext context, IDateTimeProvider dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context, _dateTime);
    public IGenericRepository<Goal> Goals => _goals ??= new GenericRepository<Goal>(_context);
    public IGenericRepository<TaskItem> Tasks => _tasks ??= new GenericRepository<TaskItem>(_context);
    public IGenericRepository<Transaction> Transactions => _transactions ??= new GenericRepository<Transaction>(_context);
    public IGenericRepository<HealthLog> HealthLogs => _healthLogs ??= new GenericRepository<HealthLog>(_context);
    public IGenericRepository<StudyMaterial> StudyMaterials => _studyMaterials ??= new GenericRepository<StudyMaterial>(_context);
    public IGenericRepository<StudyNote> StudyNotes => _studyNotes ??= new GenericRepository<StudyNote>(_context);
    public IGenericRepository<Quiz> Quizzes => _quizzes ??= new GenericRepository<Quiz>(_context);
    public IGenericRepository<CareerProfile> CareerProfiles => _careerProfiles ??= new GenericRepository<CareerProfile>(_context);
    public IGenericRepository<Recommendation> Recommendations => _recommendations ??= new GenericRepository<Recommendation>(_context);
    public IGenericRepository<AiHistoryEntry> AiHistory => _aiHistory ??= new GenericRepository<AiHistoryEntry>(_context);
    public IGenericRepository<StoredFile> Files => _files ??= new GenericRepository<StoredFile>(_context);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Транзакция уже открыта.");

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Нет открытой транзакции для фиксации.");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();

        // DbContext управляется контейнером DI (Scoped), здесь его не освобождаем.
        GC.SuppressFinalize(this);
    }
}
