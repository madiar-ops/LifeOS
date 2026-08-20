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

    /// <summary>
    /// Выполняет операцию в одной транзакции.
    ///
    /// Единый метод вместо тройки Begin/Commit/Rollback — вынужденное решение:
    /// в БД включён EnableRetryOnFailure (ADR 19), а стратегия повторов
    /// несовместима с транзакцией, открытой вручную. При сетевом сбое EF Core
    /// повторил бы только упавший вызов, а не всю транзакцию, и часть изменений
    /// применилась бы дважды. Здесь стратегия повторяет операцию целиком.
    ///
    /// Коммит выполняется автоматически при успешном завершении,
    /// откат — при любом исключении.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ExecuteInTransactionAsync(Func{CancellationToken, Task}, CancellationToken)" />
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default);
}
