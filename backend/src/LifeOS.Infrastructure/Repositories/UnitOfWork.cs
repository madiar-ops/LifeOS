using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;

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

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        => ExecuteInTransactionAsync<object?>(
            async ct =>
            {
                await operation(ct);
                return null;
            },
            cancellationToken);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
    Func<CancellationToken, Task<TResult>> operation,
    CancellationToken cancellationToken = default)
{
    var strategy = _context.Database.CreateExecutionStrategy();

    return await strategy.ExecuteAsync(
        state: operation,
        operation: async (context, operation, ct) =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(ct);

            try
            {
                var result = await operation(ct);

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        },
        verifySucceeded: null,
        cancellationToken: cancellationToken);
}
    public ValueTask DisposeAsync()
    {
        // Транзакции живут внутри ExecuteInTransactionAsync и освобождаются там же.
        // DbContext управляется контейнером DI (Scoped) — здесь его не трогаем.
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
