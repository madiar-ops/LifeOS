using System.Linq.Expressions;
using LifeOS.Application.Common;
using LifeOS.Domain.Common;

namespace LifeOS.Application.Interfaces.Repositories;

/// <summary>
/// Обобщённый репозиторий для чтения и изменения сущностей.
///
/// ВАЖНО: методы Add/Update/Remove только помечают изменения в контексте.
/// Фиксация в БД происходит ТОЛЬКО через IUnitOfWork.SaveChangesAsync —
/// это позволяет изменить несколько сущностей в одной транзакции.
/// </summary>
public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<T>> ListPagedAsync(
        PaginationParams pagination,
        Expression<Func<T, bool>>? predicate = null,
        Expression<Func<T, object>>? orderBy = null,
        bool descending = true,
        CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);

    /// <summary>
    /// Точка расширения для сложных запросов (агрегаты Dashboard, Include-цепочки).
    /// Используется только внутри слоя Application — наружу IQueryable не выходит.
    /// </summary>
    IQueryable<T> Query(bool asNoTracking = true);
}
