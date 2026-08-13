using LifeOS.Domain.Entities;

namespace LifeOS.Application.Interfaces.Repositories;

/// <summary>Специфичные для User операции, которых нет в обобщённом репозитории.</summary>
public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>Поиск по email без учёта регистра.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Пользователь вместе с активными refresh-токенами (для сценария logout-all).</summary>
    Task<User?> GetWithRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}
