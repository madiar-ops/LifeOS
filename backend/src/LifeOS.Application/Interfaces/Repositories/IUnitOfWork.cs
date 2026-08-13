using LifeOS.Domain.Entities;

namespace LifeOS.Application.Interfaces.Repositories;

/// <summary>
/// Единая точка доступа к репозиториям и к фиксации изменений.
/// Гарантирует, что все репозитории работают в одном DbContext,
/// а значит — в одной транзакции.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IUserRepository Users { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IGenericRepository<Goal> Goals { get; }
    IGenericRepository<TaskItem> Tasks { get; }
    IGenericRepository<Transaction> Transactions { get; }
    IGenericRepository<HealthLog> HealthLogs { get; }
    IGenericRepository<StudyMaterial> StudyMaterials { get; }
    IGenericRepository<StudyNote> StudyNotes { get; }
    IGenericRepository<Quiz> Quizzes { get; }
    IGenericRepository<CareerProfile> CareerProfiles { get; }
    IGenericRepository<Recommendation> Recommendations { get; }
    IGenericRepository<AiHistoryEntry> AiHistory { get; }
    IGenericRepository<StoredFile> Files { get; }

    /// <summary>Фиксирует все накопленные изменения. Возвращает количество затронутых строк.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Явная транзакция — нужна там, где несколько SaveChanges должны быть атомарны.</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
